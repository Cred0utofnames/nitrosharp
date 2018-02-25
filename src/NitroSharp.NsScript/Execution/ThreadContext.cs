﻿using NitroSharp.NsScript.Symbols;
using System;
using System.Collections.Generic;

namespace NitroSharp.NsScript.Execution
{
    public sealed class ThreadContext
    {
        private readonly Stack<Frame> _callstack;

        internal ThreadContext(string name, MergedSourceFileSymbol module, InvocableSymbol entryPoint)
        {
            Name = name;
            EntryPoint = entryPoint;
            Stack = new Stack<ConstantValue>();
            _callstack = new Stack<Frame>();
            _callstack.Push(new Frame(module, entryPoint));
        }

        public string Name { get; }
        public InvocableSymbol EntryPoint { get; }

        public bool IsSuspended { get; internal set; }
        public TimeSpan SleepTimeout { get; internal set; }
        public TimeSpan SuspensionTime { get; internal set; }

        public bool DoneExecuting => _callstack.Count == 0;
        internal Frame CurrentFrame => _callstack.Peek();
        internal Stack<ConstantValue> Stack { get; }

        internal void PopFrame() => _callstack.Pop();
        internal void PushFrame(Frame frame)
        {
            _callstack.Push(frame);
        }

        public void Call(InvocableSymbol symbol)
        {
            PushFrame(new Frame(CurrentFrame.Module, symbol));
        }
    }
}
