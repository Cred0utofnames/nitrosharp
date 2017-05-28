﻿using CommitteeOfZero.Nitro.Foundation;
using CommitteeOfZero.Nitro.Foundation.Content;
using System;
using System.Diagnostics;

namespace CommitteeOfZero.Nitro.Audio
{
    public sealed class SoundComponent : Component
    {
        public SoundComponent(AssetRef audioFile, AudioKind kind)
        {
            AudioFile = audioFile;
            Kind = kind;
            Volume = 1.0f;
        }

        public AssetRef AudioFile { get; }
        public AudioKind Kind { get; }

        public float Volume { get; set; }
        public TimeSpan LoopStart { get; private set; }
        public TimeSpan LoopEnd { get; private set; }
        public bool Looping { get; set; }

        public void SetLoop(TimeSpan loopStart, TimeSpan loopEnd)
        {
            Debug.Assert(loopEnd > loopStart);

            LoopStart = loopStart;
            LoopEnd = loopEnd;
        }

        public override string ToString() => $"Sound '{AudioFile}', kind = {Kind.ToString()}";
    }
}