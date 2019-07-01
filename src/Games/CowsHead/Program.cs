﻿using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NitroSharp.Launcher;

using FFmpegLibs = FFmpeg.AutoGen.NativeLibs;

namespace CowsHead
{
    class Program
    {
        private static IntPtr s_libfreetype;
        private static IntPtr s_libopenal;
        private static IntPtr s_libavcodec;
        private static IntPtr s_libavformat;
        private static IntPtr s_libavdevice;
        private static IntPtr s_libavutil;
        private static IntPtr s_libavfilter;
        private static IntPtr s_libswresample;
        private static IntPtr s_libswscale;

        private enum Platform
        {
            Windows,
            MacOS,
            Linux
        }

        static Task Main()
        {
            

#if PORTABLE
            LoadNativeDependencies();
            NativeLibrary.SetDllImportResolver(typeof(NitroSharp.Game).Assembly, ResolveDllImport);
            NativeLibrary.SetDllImportResolver(typeof(FreeTypeBindings.FT).Assembly, ResolveDllImport);
            NativeLibrary.SetDllImportResolver(typeof(OpenAL.NativeLib).Assembly, ResolveDllImport);
            NativeLibrary.SetDllImportResolver(typeof(FFmpeg.AutoGen.ffmpeg).Assembly, ResolveDllImport);
#endif
            return GameLauncher.Launch("Game.json");
        }

        private static void LoadNativeDependencies()
        {
            Platform platform;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                platform = Platform.Windows;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                platform = Platform.MacOS;
            }
            else
            {
                platform = Platform.Linux;
            }

            string freetype, openal, avcodec, avformat, avdevice, avutil, avfilter, swresample, swscale;
            switch (platform)
            {
                case Platform.Windows:
                    freetype = FreeTypeBindings.NativeLib.WindowsName;
                    openal = OpenAL.NativeLib.WindowsName;
                    avcodec = FFmpegLibs.avcodec.WindowsName;
                    avformat = FFmpegLibs.avformat.WindowsName;
                    avdevice = FFmpegLibs.avdevice.WindowsName;
                    avutil = FFmpegLibs.avutil.WindowsName;
                    avfilter = FFmpegLibs.avfilter.WindowsName;
                    swresample = FFmpegLibs.swresample.WindowsName;
                    swscale = FFmpegLibs.swscale.WindowsName;
                    break;
                case Platform.MacOS:
                    freetype = FreeTypeBindings.NativeLib.OsxName;
                    openal = OpenAL.NativeLib.OsxName;
                    avcodec = FFmpegLibs.avcodec.OsxName;
                    avformat = FFmpegLibs.avformat.OsxName;
                    avdevice = FFmpegLibs.avdevice.OsxName;
                    avutil = FFmpegLibs.avutil.OsxName;
                    avfilter = FFmpegLibs.avfilter.OsxName;
                    swresample = FFmpegLibs.swresample.OsxName;
                    swscale = FFmpegLibs.swscale.OsxName;
                    break;
                case Platform.Linux:
                    freetype = FreeTypeBindings.NativeLib.LinuxName;
                    openal = OpenAL.NativeLib.LinuxName;
                    avcodec = FFmpegLibs.avcodec.LinuxName;
                    avformat = FFmpegLibs.avformat.LinuxName;
                    avdevice = FFmpegLibs.avdevice.LinuxName;
                    avutil = FFmpegLibs.avutil.LinuxName;
                    avfilter = FFmpegLibs.avfilter.LinuxName;
                    swresample = FFmpegLibs.swresample.LinuxName;
                    swscale = FFmpegLibs.swscale.LinuxName;
                    break;
                default:
                    throw new PlatformNotSupportedException("Your OS is not yet supported.");
            }

            Assembly ffmpegBindings = typeof(FFmpeg.AutoGen.ffmpeg).Assembly;
            s_libfreetype = NativeLibrary.Load(freetype, typeof(FreeTypeBindings.NativeLib).Assembly, null);
            s_libopenal = NativeLibrary.Load(openal, typeof(OpenAL.NativeLib).Assembly, null);
            s_libavcodec = NativeLibrary.Load(avcodec, ffmpegBindings, null);
            s_libavformat = NativeLibrary.Load(avformat, ffmpegBindings, null);
            s_libavdevice = NativeLibrary.Load(avdevice, ffmpegBindings, null);
            s_libavutil = NativeLibrary.Load(avutil, ffmpegBindings, null);
            s_libavfilter = NativeLibrary.Load(avfilter, ffmpegBindings, null);
            s_libswresample = NativeLibrary.Load(swresample, ffmpegBindings, null);
            s_libswscale = NativeLibrary.Load(swscale, ffmpegBindings, null);
        }

        private static IntPtr ResolveDllImport(
            string libraryName,
            Assembly assembly,
            DllImportSearchPath? searchPath)
        {
            return libraryName switch
            {
                FreeTypeBindings.NativeLib.PortableName => s_libfreetype,
                OpenAL.NativeLib.PortableName => s_libopenal,
                FFmpegLibs.avcodec.PortableName => s_libavcodec,
                FFmpegLibs.avformat.PortableName => s_libavformat,
                FFmpegLibs.avdevice.PortableName => s_libavdevice,
                FFmpegLibs.avutil.PortableName => s_libavutil,
                FFmpegLibs.avfilter.PortableName => s_libavfilter,
                FFmpegLibs.swresample.PortableName => s_libswresample,
                FFmpegLibs.swscale.PortableName => s_libswscale,
                _ => IntPtr.Zero
            };
        }
    }
}
