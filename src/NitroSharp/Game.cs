﻿using NitroSharp.Content;
using NitroSharp.Graphics;
using NitroSharp.Graphics.Systems;
using NitroSharp.Media;
using NitroSharp.Media.Decoding;
using NitroSharp.NsScript;
using NitroSharp.NsScript.Execution;
using NitroSharp.Primitives;
using NitroSharp.Text;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Veldrid;
using Veldrid.StartupUtilities;
using System.Runtime.InteropServices;
using NitroSharp.Dialogue;
using System.Collections.Concurrent;
using NitroSharp.Input;
using System.Collections.Generic;
using NitroSharp.Animation;

namespace NitroSharp
{
    public sealed class Game : IDisposable
    {
        private bool IsMultithreaded = false;
        private bool UseWicOnWindows = false;

        private readonly Stopwatch _gameTimer;
        private readonly Queue<Message> _messageQueue;
        private readonly Configuration _configuration;
        private readonly CancellationTokenSource _shutdownCancellation;

        private readonly GameWindow _window;
        private volatile bool _needsResize;
        private volatile bool _surfaceDestroyed;
        private GraphicsDevice _graphicsDevice;
        private Swapchain _swapchain;
        private readonly TaskCompletionSource<int> _initializingGraphics;

        private readonly InputTracker _inputTracker;

        private readonly string _nssFolder;
        private NsScriptInterpreter _nssInterpreter;
        private readonly NsBuiltins _builtinFunctions;
        private Task _interpreterProc;
        private volatile bool _syncToFuture;

        private VideoFrameConverter _frameConverter;
        private SharpDX.WIC.ImagingFactory _wicFactory;
        private AudioDevice _audioDevice;
        private AudioSourcePool _audioSourcePool;

        private World _presentWorld;
        private World _futureWorld;

        private DialogueSystem _dialogueSystem;
        private DialogueSystemInput _dialogueSystemInput;

        private RenderSystem _renderSystem;
        private AudioSystem _audioSystem;
        private AnimationProcessor _animationProcessor;
        private bool _syncToPresent;

        public Game(GameWindow window, Configuration configuration)
        {
            _shutdownCancellation = new CancellationTokenSource();
            _initializingGraphics = new TaskCompletionSource<int>();

            _configuration = configuration;
            _nssFolder = Path.Combine(configuration.ContentRoot, "nss");
            _window = window;
            _window.Mobile_SurfaceCreated += OnSurfaceCreated;
            _window.Resized += OnWindowResized;
            _window.Mobile_SurfaceDestroyed += OnSurfaceDestroyed;

            _gameTimer = new Stopwatch();
            _messageQueue = new Queue<Message>();
            _builtinFunctions = new NsBuiltins(this);
            _inputTracker = new InputTracker(window);
            InterpreterLoopTasks = new ConcurrentQueue<Action>();
        }

        internal Queue<Message> MessageQueue => _messageQueue;
        internal ContentManager Content { get; private set; }
        internal FontService FontService { get; private set; }

        internal AudioDevice AudioDevice => _audioDevice;
        internal AudioSourcePool AudioSourcePool => _audioSourcePool;

        internal ConcurrentQueue<Action> InterpreterLoopTasks { get; }

        public async Task Run(bool useDedicatedThread = false)
        {
            Task loadScriptTask = Task.Run((Action)LoadStartupScript);
            Task initializeAudio = Task.Run((Action)SetupAudio);

            await Task.WhenAll(_initializingGraphics.Task, initializeAudio);

            CreateServices();
            CreateGameWorld();
            await loadScriptTask;

            _dialogueSystem = new DialogueSystem(_presentWorld, _inputTracker, _nssInterpreter, Content);
            _animationProcessor = new AnimationProcessor(_presentWorld, _nssInterpreter);
            StartInterpreter();

            await RunMainLoop(useDedicatedThread);
        }

        private Task RunMainLoop(bool useDedicatedThread = false)
        {
            if (useDedicatedThread)
            {
                return Task.Factory.StartNew(MainLoop, TaskCreationOptions.LongRunning);
            }
            else
            {
                try
                {
                    MainLoop();
                }
                catch (TaskCanceledException)
                {
                }

                return Task.FromResult(0);
            }
        }

        private void MainLoop()
        {
            _gameTimer.Start();

            float prevFrameTicks = 0.0f;
            while (!_shutdownCancellation.IsCancellationRequested && _window.Exists)
            {
                if (_surfaceDestroyed)
                {
                    HandleSurfaceDestroyed();
                    return;
                }
                if (_needsResize)
                {
                    Size newSize = _window.Size;
                    _swapchain.Resize(newSize.Width, newSize.Height);
                    _needsResize = false;
                }

                long currentFrameTicks = _gameTimer.ElapsedTicks;
                float deltaMilliseconds = (currentFrameTicks - prevFrameTicks) / Stopwatch.Frequency * 1000.0f;
                prevFrameTicks = currentFrameTicks;

                try
                {
                    Tick(deltaMilliseconds);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        private void Tick(float deltaMilliseconds)
        {
            bool mainThreadWaiting = false;
            _inputTracker.Update(deltaMilliseconds);
            if (IsMultithreaded)
            {
                if (_interpreterProc.IsFaulted)
                {
                    throw _interpreterProc.Exception.InnerException;
                }
                else if (_interpreterProc.IsCompleted)
                {
                    Exit();
                }
                if (_syncToFuture)
                {
                    _futureWorld.MergeChanges(_presentWorld);
                    _futureWorld.FlushEvents();
                    _syncToFuture = false;
                    mainThreadWaiting = _nssInterpreter.MainThread.IsSuspended
                        && _nssInterpreter.MainThread.SleepTimeout != TimeSpan.MaxValue;
                    ProcessMessages();
                }
                else if (_syncToPresent)
                {
                    _presentWorld.MergeChanges(_futureWorld);
                    _syncToPresent = false;
                }
            }
            else
            {
                _nssInterpreter.RefreshThreadState();
                _nssInterpreter.ProcessPendingThreadActions();

                while (InterpreterLoopTasks.TryDequeue(out Action a))
                {
                    a.Invoke();
                }

                _nssInterpreter.Run(CancellationToken.None);
                mainThreadWaiting = _nssInterpreter.MainThread.IsSuspended
                    && _nssInterpreter.MainThread.SleepTimeout != TimeSpan.MaxValue;
                ProcessMessages();
            }

            _presentWorld.FlushDetachedAnimations();
            var animProcessorOutput = _animationProcessor.ProcessAnimations(deltaMilliseconds);
            bool blockInput = mainThreadWaiting || animProcessorOutput.BlockingAnimationCount > 0;
            _dialogueSystemInput.AcceptUserInput = !blockInput;
            if (_dialogueSystem.AdvanceDialogueState(ref _dialogueSystemInput))
            {
                _dialogueSystemInput.Command = DialogueSystemCommand.HandleInput;
            }

            _audioSystem.UpdateAudioSources();
            _renderSystem.Update(deltaMilliseconds);

            if (_window.Exists)
            {
                try
                {
                    _renderSystem.Present();
                }
                catch (VeldridException e) when (e.Message == "The Swapchain's underlying surface has been lost.")
                {
                    return;
                }
            }

            _presentWorld.FlushEvents();
        }

        private void ProcessMessages()
        {
            while (_messageQueue.Count > 0)
            {
                Message message = _messageQueue.Dequeue();
                switch (message)
                {
                    case BeginDialogueBlockMessage beginBlockMsg:
                        _dialogueSystemInput.TextEntity = beginBlockMsg.TextEntity;
                        break;
                    case BeginDialogueLineMessage beginLineMsg:
                        _dialogueSystemInput.Command = DialogueSystemCommand.BeginDialogue;
                        _dialogueSystemInput.DialogueLine = beginLineMsg.DialogueLine;
                        break;
                }
            }
        }

        private void StartInterpreter()
        {
            if (IsMultithreaded)
            {
                _interpreterProc = Task.Factory.StartNew(InterpreterLoop, TaskCreationOptions.LongRunning);
            }
        }

        private void InterpreterLoop()
        {
            while (!_shutdownCancellation.IsCancellationRequested)
            {
                while (_syncToFuture || _syncToPresent)
                {
                    Thread.Sleep(5);
                }

                Thread.Sleep(1);

                bool threadStateChanged = _nssInterpreter.RefreshThreadState();
                if (threadStateChanged || _nssInterpreter.ProcessPendingThreadActions())
                {
                    _syncToPresent = true;
                    continue;
                }

                while (InterpreterLoopTasks.TryDequeue(out Action a))
                {
                    a.Invoke();
                }

                if (_nssInterpreter.Run(CancellationToken.None))
                {
                    _syncToFuture = true;
                }

                //if (!_nssInterpreter.Threads.Any()) { return; }
            }
        }

        private void LoadStartupScript()
        {
            _nssInterpreter = new NsScriptInterpreter(LocateScript, _builtinFunctions);
            ThreadContext mainThread = _nssInterpreter.CreateThread("__MAIN", _configuration.StartupScript, "main");
        }

        private Stream LocateScript(SourceFileReference fileRef)
        {
            return File.OpenRead(Path.Combine(_nssFolder, fileRef.FilePath.Replace("nss/", string.Empty)));
        }

        private void OnSurfaceCreated(SwapchainSource swapchainSource)
        {
            bool resuming = _initializingGraphics.Task.IsCompleted;
            SetupGraphics(swapchainSource);
            _initializingGraphics.TrySetResult(0);

            if (resuming)
            {
                if (_graphicsDevice.BackendType == GraphicsBackend.OpenGLES)
                {
                    Content.SetGraphicsDevice(_graphicsDevice);
                    RecreateGraphicsResources();
                }

                RunMainLoop(true);
            }
        }

        private void SetupGraphics(SwapchainSource swapchainSource)
        {
            var options = new GraphicsDeviceOptions(false, null, _configuration.EnableVSync);
#if DEBUG
            options.Debug = true;
#endif
            GraphicsBackend backend = _configuration.PreferredGraphicsBackend ?? VeldridStartup.GetPlatformDefaultBackend();
            var swapchainDesc = new SwapchainDescription(swapchainSource,
                    (uint)_configuration.WindowWidth, (uint)_configuration.WindowHeight,
                    options.SwapchainDepthFormat, options.SyncToVerticalBlank);

            if (backend == GraphicsBackend.OpenGLES || backend == GraphicsBackend.OpenGL)
            {
                _graphicsDevice = _window is DesktopWindow desktopWindow
                    ? VeldridStartup.CreateDefaultOpenGLGraphicsDevice(options, desktopWindow.SdlWindow, backend)
                    : GraphicsDevice.CreateOpenGLES(options, swapchainDesc);
                _swapchain = _graphicsDevice.MainSwapchain;
            }
            else
            {
                if (_graphicsDevice == null)
                {
                    _graphicsDevice = CreateDeviceWithoutSwapchain(backend, options);
                }
                _swapchain = _graphicsDevice.ResourceFactory.CreateSwapchain(ref swapchainDesc);
            }
        }

        private GraphicsDevice CreateDeviceWithoutSwapchain(GraphicsBackend backend, GraphicsDeviceOptions options)
        {
            switch (backend)
            {
                case GraphicsBackend.Direct3D11:
                    return GraphicsDevice.CreateD3D11(options);
                case GraphicsBackend.Vulkan:
                    return GraphicsDevice.CreateVulkan(options);
                case GraphicsBackend.Metal:
                    return GraphicsDevice.CreateMetal(options);
                default:
                    return null;
            }
        }

        private void SetupAudio()
        {
            var audioParameters = AudioParameters.Default;
            var backend = _configuration.PreferredAudioBackend ?? AudioDevice.GetPlatformDefaultBackend();
            _audioDevice = AudioDevice.Create(backend, audioParameters);
            _audioSourcePool = new AudioSourcePool(_audioDevice);
        }

        private void CreateServices()
        {
            Content = CreateContentManager();
            Content.SetGraphicsDevice(_graphicsDevice);
            FontService = new FontService();
            FontService.RegisterFonts(Directory.EnumerateFiles("Fonts"));
        }

        private ContentManager CreateContentManager()
        {
            var content = new ContentManager(_configuration.ContentRoot);
            ContentLoader textureLoader = null;
            ContentLoader textureDataLoader = null;

            if (UseWicOnWindows && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _wicFactory = new SharpDX.WIC.ImagingFactory();
                textureLoader = new WicTextureLoader(content, _wicFactory);
                textureDataLoader = new WicTextureDataLoader(content, _wicFactory);
            }
            else
            {
                textureLoader = new FFmpegTextureLoader(content);
                textureDataLoader = new FFmpegTextureDataLoader(content);
            }

            content.RegisterContentLoader(typeof(BindableTexture), textureLoader);
            content.RegisterContentLoader(typeof(TextureData), textureDataLoader);

            _frameConverter = new VideoFrameConverter();
            var mediaFileLoader = new MediaClipLoader(content, _frameConverter, _audioDevice.AudioParameters);
            content.RegisterContentLoader(typeof(MediaPlaybackSession), mediaFileLoader);

            return content;
        }

        private void CreateGameWorld()
        {
            _presentWorld = new World(WorldKind.Primary);
            World scriptingWorld = _presentWorld;
            if (IsMultithreaded)
            {
                _futureWorld = new World(WorldKind.Secondary);
                scriptingWorld = _futureWorld;
            }

            _builtinFunctions.SetWorld(scriptingWorld);


            _renderSystem = new RenderSystem(
                _presentWorld, _graphicsDevice, _swapchain,
                Content, FontService, _configuration);

            _audioSystem = new AudioSystem(_presentWorld, Content, _audioSourcePool);
        }

        private void OnWindowResized()
        {
            _needsResize = true;
        }

        private void OnSurfaceDestroyed()
        {
            _surfaceDestroyed = true;
        }

        private void HandleSurfaceDestroyed()
        {
            if (_graphicsDevice.BackendType == GraphicsBackend.OpenGLES)
            {
                DestroyDeviceResources();
                _graphicsDevice.Dispose();
                _graphicsDevice = null;
                _swapchain = null;
            }
            else
            {
                _swapchain.Dispose();
                _swapchain = null;
            }

            _surfaceDestroyed = false;
            _window.Mobile_HandledSurfaceDestroyed.Set();
        }

        private void RecreateGraphicsResources()
        {
            Content.ReloadTextures();
            // _renderSystem.CreateDeviceResources(_graphicsDevice, _swapchain);
        }

        private void DestroyDeviceResources()
        {
            // _renderSystem.DestroyDeviceResources();
            Content.DestroyTextures();
        }

        public void Exit()
        {
            _shutdownCancellation.Cancel();
        }

        public void Dispose()
        {
            _renderSystem.Dispose();
            Content.Dispose();
            FontService.Dispose();
            _wicFactory?.Dispose();
            _swapchain.Dispose();
            _graphicsDevice.Dispose();
            _audioDevice.Dispose();
            _frameConverter.Dispose();
        }
    }
}
