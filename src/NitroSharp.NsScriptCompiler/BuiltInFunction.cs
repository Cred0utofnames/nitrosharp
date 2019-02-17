﻿namespace NitroSharp.NsScript
{
    public enum BuiltInFunction : byte
    {
        Wait,
        WaitKey,
        Request,
        SetAlias,
        Delete,
        CreateProcess,
        LoadImage,
        CreateColor,
        CreateTexture,
        CreateClipTexture,
        CreateSound,

        Fade,
        Move,
        Zoom,
        Rotate,
        MoveCube,

        SetVolume,
        CreateWindow,
        LoadText,
        WaitText,
        SetLoop,
        SetLoopPoint,
        DrawTransition,

        DurationTime,
        PassageTime,
        RemainTime,
        ImageHorizon,
        ImageVertical,
        Random,
        SoundAmplitude,
        Platform,
        ModuleFileName,
        String,
        Time,
        Integer,
        DateTime,
        CursorPosition,

        SetFont,
        SetNextFocus,
        Position,
        MoveCursor,

        CreateCube,
        SetFov,
        CreateMovie,
        WaitPlay,

        WaitAction,
        WaitMove,
        LoadFont,
        LoadColor,
        
        CreateText,
        CreateChoice,
        CreateMask,
        CreateName,
        LoadFile,
        UnloadFile,

        Shake,
        SetBlur,
        CreateEffect,
        SetTone,
        SetShade,

        Save,
        Load,
        ExistSave,
        ClearScore,
        Reset,
        DeleteSaveFile,
        MountSavedata,
        AvailableFile,
        Escape,
        CreateBacklog,
        SetBacklog,
        EnableBacklog,
        ClearBacklog,

        CreateScrollbar,
        SetScrollbar,
        ScrollbarValue,
        SetScrollSpeed,

        SetFrequency,
        SetPan,
        Draw,
        SetVertex,
        SetStream,
        Exit,

        //XBOX360_LockVideo,
        //XBOX360_Presence,
        //XBOX360_AwardGameIcon,
        //XBOX360_Achieved,
        //XBOX360_InitUser,
        //XBOX360_IsSignin,
        //XBOX360_CheckStorage,
        //XBOX360_SelectStorage,
        //XBOX360_ExistContent,
        //XBOX360_StorageSize,
        //XBOX360_UserIndex,
        //XBOX360_PadTrigger,
        //XBOX360_CurrentStorage,

        log,
        assert,
        asserteq,
        fail,
        fail_msg
    }
}
