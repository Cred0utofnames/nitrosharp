﻿using System.Collections.Immutable;

namespace NitroSharp.NsScriptNew.Syntax
{
    public abstract class MemberDeclarationSyntax : SyntaxNode
    {
        protected MemberDeclarationSyntax(Spanned<string> name, BlockSyntax body)
        {
            Name = name;
            Body = body;
        }

        public Spanned<string> Name { get; }
        public BlockSyntax Body { get; }

        public override SyntaxNode GetNodeSlot(int index)
        {
            switch (index)
            {
                case 0: return Body;
                default: return null;
            }
        }
    }

    public sealed class ChapterDeclarationSyntax : MemberDeclarationSyntax
    {
        internal ChapterDeclarationSyntax(Spanned<string> name, BlockSyntax body)
            : base(name, body)
        {
        }

        public override SyntaxNodeKind Kind => SyntaxNodeKind.ChapterDeclaration;

        public override void Accept(SyntaxVisitor visitor)
        {
            visitor.VisitChapter(this);
        }

        public override TResult Accept<TResult>(SyntaxVisitor<TResult> visitor)
        {
            return visitor.VisitChapter(this);
        }
    }

    public sealed class SceneDeclarationSyntax : MemberDeclarationSyntax
    {
        internal SceneDeclarationSyntax(Spanned<string> name, BlockSyntax body)
            : base(name, body)
        {
        }

        public override SyntaxNodeKind Kind => SyntaxNodeKind.SceneDeclaration;

        public override void Accept(SyntaxVisitor visitor)
        {
            visitor.VisitScene(this);
        }

        public override TResult Accept<TResult>(SyntaxVisitor<TResult> visitor)
        {
            return visitor.VisitScene(this);
        }
    }

    public sealed class FunctionDeclarationSyntax : MemberDeclarationSyntax
    {
        internal FunctionDeclarationSyntax(
            Spanned<string> name,
            ImmutableArray<ParameterSyntax> parameters,
            BlockSyntax body) : base(name, body)
        {
            Parameters = parameters;
        }

        public ImmutableArray<ParameterSyntax> Parameters { get; }
        public override SyntaxNodeKind Kind => SyntaxNodeKind.Function;

        public override void Accept(SyntaxVisitor visitor)
        {
            visitor.VisitFunction(this);
        }

        public override TResult Accept<TResult>(SyntaxVisitor<TResult> visitor)
        {
            return visitor.VisitFunction(this);
        }
    }

    public sealed class ParameterSyntax : SyntaxNode
    {
        internal ParameterSyntax(Spanned<string> name)
        {
            Name = name;
        }

        public Spanned<string> Name { get; }
        public override SyntaxNodeKind Kind => SyntaxNodeKind.Parameter;

        public override void Accept(SyntaxVisitor visitor)
        {
            visitor.VisitParameter(this);
        }

        public override TResult Accept<TResult>(SyntaxVisitor<TResult> visitor)
        {
            return visitor.VisitParameter(this);
        }
    }

    public sealed class DialogueBlockSyntax : StatementSyntax
    {
        internal DialogueBlockSyntax(
            string name, string associatedBox,
            ImmutableArray<StatementSyntax> parts)
        {
            Name = name;
            AssociatedBox = associatedBox;
            Parts = parts;
        }

        public string Name { get; }
        public string AssociatedBox { get; }
        public ImmutableArray<StatementSyntax> Parts { get; }

        public override SyntaxNodeKind Kind => SyntaxNodeKind.DialogueBlock;

        public override void Accept(SyntaxVisitor visitor)
        {
            visitor.VisitDialogueBlock(this);
        }

        public override TResult Accept<TResult>(SyntaxVisitor<TResult> visitor)
        {
            return visitor.VisitDialogueBlock(this);
        }
    }
}