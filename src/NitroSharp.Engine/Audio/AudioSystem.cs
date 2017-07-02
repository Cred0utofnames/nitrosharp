﻿using NitroSharp.Foundation;
using NitroSharp.Foundation.Audio;
using NitroSharp.Foundation.Content;
using System.Collections.Generic;
using System;
using System.Runtime.InteropServices;

namespace NitroSharp.Audio
{
    public sealed class AudioSystem : EntityProcessingSystem, IDisposable
    {
        public static int Amplitude { get; set; }

        private static uint VoiceBufferSize = 4400;

        private readonly AudioEngine _audioEngine;
        private uint _defaultBufferSize;

        private readonly AudioSource _voiceAudioSource;
        private Dictionary<SoundComponent, AudioSource> _audioSources;
        private Queue<AudioSource> _freeAudioSources;

        public AudioSystem(AudioEngine audioEngine)
        {
            _audioEngine = audioEngine;

            _voiceAudioSource = _audioEngine.ResourceFactory.CreateAudioSource(VoiceBufferSize);
            _voiceAudioSource.PreviewBufferSent += _voiceAudioSource_PreviewBufferSent;
            _audioSources = new Dictionary<SoundComponent, AudioSource>();
            _freeAudioSources = new Queue<AudioSource>();

            _defaultBufferSize = (uint)(_audioEngine.SampleRate * _audioEngine.ChannelCount);
        }

        protected override void DeclareInterests(ISet<Type> interests)
        {
            interests.Add(typeof(SoundComponent));
        }

        public override void OnRelevantEntityAdded(Entity entity)
        {
            var sound = entity.GetComponent<SoundComponent>();

            //string path = sound.AudioFile.Id;
            //if (!_content.Exists(path))
            //{
            //    path += ".ogg";
            //}

            var stream = sound.Source.Asset;
            var audioSource = GetFreeAudioSource(sound.Kind);
            if (sound.Kind == AudioKind.Voice)
            {
                audioSource.Stop();
            }

            audioSource.SetStream(stream);
            if (sound.Kind != AudioKind.Voice)
            {
                _audioSources[sound] = audioSource;
            }
        }

        private void _voiceAudioSource_PreviewBufferSent(object sender, AudioBuffer e)
        {
            short firstSample = Marshal.ReadInt16(e.StartPointer, 0);
            short secondSample = Marshal.ReadInt16(e.StartPointer, e.Position / 4);
            short thirdSample = Marshal.ReadInt16(e.StartPointer, e.Position / 4 + e.Position / 2);
            short fourthSample = Marshal.ReadInt16(e.StartPointer, e.Position - 2);

            double amplitude = (Math.Abs(firstSample) + Math.Abs(secondSample) + Math.Abs(thirdSample) + Math.Abs(fourthSample)) / 4.0f;
            Amplitude = (int)Math.Round(amplitude);
        }

        public override void OnRelevantEntityRemoved(Entity entity)
        {
            var sound = entity.GetComponent<SoundComponent>();
            StopAndRemove(sound);
        }

        private void StopAndRemove(SoundComponent sound)
        {
            var audioSource = GetAssociatedSource(sound);
            audioSource.Stop();

            if (sound.Kind != AudioKind.Voice)
            {
                _audioSources.Remove(sound);
                _freeAudioSources.Enqueue(audioSource);
            }
        }

        private AudioSource GetAssociatedSource(SoundComponent sound)
        {
            return sound.Kind == AudioKind.Voice ? _voiceAudioSource : _audioSources[sound];
        }

        private AudioSource GetFreeAudioSource(AudioKind audioKind)
        {
            if (audioKind == AudioKind.Voice)
            {
                return _voiceAudioSource;
            }

            return _freeAudioSources.Count > 0 ? _freeAudioSources.Dequeue()
                : _audioEngine.ResourceFactory.CreateAudioSource(_defaultBufferSize);
        }

        public override void Process(Entity entity, float deltaMilliseconds)
        {
            var sound = entity.GetComponent<SoundComponent>();
            var audioSource = GetAssociatedSource(sound);
            audioSource.Volume = GetVolumeMultiplier(sound) * sound.Volume;

            if (sound.Volume > 0 && audioSource.Status != AudioSourceStatus.Playing)
            {
                audioSource.Play();
            }
            else if (sound.Volume == 0 && audioSource.Status == AudioSourceStatus.Playing)
            {
                audioSource.Stop();
            }

            if (sound.Looping && !audioSource.CurrentStream.Looping)
            {
                if (sound.LoopEnd.TotalSeconds > 0)
                {
                    audioSource.CurrentStream.SetLoop(sound.LoopStart, sound.LoopEnd);
                }
                else
                {
                    audioSource.CurrentStream.SetLoop();
                }
            }
        }

        private static float GetVolumeMultiplier(SoundComponent sound)
        {
            switch (sound.Kind)
            {
                case AudioKind.BackgroundMusic:
                    return 0.6f;
                case AudioKind.SoundEffect:
                    return 1.0f;
                case AudioKind.Voice:
                default:
                    return 0.75f;
            }
        }

        public void Dispose()
        {
            _audioEngine.StopAllVoices();
        }
    }
}