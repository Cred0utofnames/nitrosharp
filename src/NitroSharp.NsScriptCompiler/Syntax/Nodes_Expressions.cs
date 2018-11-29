﻿using System;
using System.Collections.Immutable;

namespace NitroSharp.NsScriptNew.Syntax
{
    public abstract class ExpressionSyntax : SyntaxNode
    {
    }

    public sealed class LiteralExpressionSyntax : ExpressionSyntax
    {
        internal LiteralExpressionSyntax(Spanned<ConstantValue> value)
        {
            Value = value;
        }

        public Spanned<ConstantValue> Value { get; }
        
        public override SyntaxNodeKind Kind => SyntaxNodeKind.LiteralExpression;

        public override void Accept(SyntaxVisitor visitor)
        {
            visitor.VisitLiteral(this);
        }

        public override TResult Accept<TResult>(SyntaxVisitor<TResult> visitor)
        {
            return visitor.VisitLiteral(this);
        }
    }

    public sealed class NameSyntax : ExpressionSyntax
    {
        internal NameSyntax(Spanned<string> name)
        {
            Name = name;
        }

        public Spanned<string> Name { get; }

        public override SyntaxNodeKind Kind => SyntaxNodeKind.NameExpression;

        public override void Accept(SyntaxVisitor visitor)
        {
            //visitor.VisitIdentifier(this);
        }

        public override TResult Accept<TResult>(SyntaxVisitor<TResult> visitor)
        {
            throw new NotImplementedException();
            //return visitor.VisitIdentifier(this);
        }
    }

    public sealed class UnaryExpressionSyntax : ExpressionSyntax
    {
        internal UnaryExpressionSyntax(
            ExpressionSyntax operand,
            Spanned<UnaryOperatorKind> operatorKind)
        {
            Operand = operand;
            OperatorKind = operatorKind;
        }

        public ExpressionSyntax Operand { get; }
        public Spanned<UnaryOperatorKind> OperatorKind { get; }

        public override SyntaxNodeKind Kind => SyntaxNodeKind.UnaryExpression;

        public override SyntaxNode GetNodeSlot(int index)
        {
            switch (index)
            {
                case 0: return Operand;
                default: return null;
            }
        }

        public override void Accept(SyntaxVisitor visitor)
        {
            visitor.VisitUnaryExpression(this);
        }

        public override TResult Accept<TResult>(SyntaxVisitor<TResult> visitor)
        {
            return visitor.VisitUnaryExpression(this);
        }
    }

    public sealed class BinaryExpressionSyntax : ExpressionSyntax
    {
        internal BinaryExpressionSyntax(
            ExpressionSyntax left,
            Spanned<BinaryOperatorKind> operatorKind,
            ExpressionSyntax right)
        {
            Left = left;
            OperatorKind = operatorKind;
            Right = right;
        }

        public ExpressionSyntax Left { get; }
        public Spanned<BinaryOperatorKind> OperatorKind { get; }
        public ExpressionSyntax Right { get; }

        public override SyntaxNodeKind Kind => SyntaxNodeKind.BinaryExpression;

        public override SyntaxNode GetNodeSlot(int index)
        {
            switch (index)
            {
                case 0: return Left;
                case 1: return Right;
                default: return null;
            }
        }

        public override void Accept(SyntaxVisitor visitor)
        {
            visitor.VisitBinaryExpression(this);
        }

        public override TResult Accept<TResult>(SyntaxVisitor<TResult> visitor)
        {
            return visitor.VisitBinaryExpression(this);
        }
    }

    public sealed class AssignmentExpressionSyntax : ExpressionSyntax
    {
        internal AssignmentExpressionSyntax(
            ExpressionSyntax target,
            Spanned<AssignmentOperatorKind> operatorKind,
            ExpressionSyntax value)
        {
            Target = target;
            OperatorKind = operatorKind;
            Value = value;
        }

        public ExpressionSyntax Target { get; }
        public Spanned<AssignmentOperatorKind> OperatorKind { get; }
        public ExpressionSyntax Value { get; }

        public override SyntaxNodeKind Kind => SyntaxNodeKind.AssignmentExpression;

        public override SyntaxNode GetNodeSlot(int index)
        {
            switch (index)
            {
                case 0: return Target;
                case 1: return Value;
                default: return null;
            }
        }

        public override void Accept(SyntaxVisitor visitor)
        {
            visitor.VisitAssignmentExpression(this);
        }

        public override TResult Accept<TResult>(SyntaxVisitor<TResult> visitor)
        {
            return visitor.VisitAssignmentExpression(this);
        }
    }

    public sealed class DeltaExpressionSyntax : ExpressionSyntax
    {
        internal DeltaExpressionSyntax(ExpressionSyntax expression)
        {
            Expression = expression;
        }

        public ExpressionSyntax Expression { get; }
        public override SyntaxNodeKind Kind => SyntaxNodeKind.DeltaExpression;

        public override SyntaxNode GetNodeSlot(int index)
        {
            switch (index)
            {
                case 0: return Expression;
                default: return null;
            }
        }

        public override void Accept(SyntaxVisitor visitor)
        {
            visitor.VisitDeltaExpression(this);
        }

        public override TResult Accept<TResult>(SyntaxVisitor<TResult> visitor)
        {
            return visitor.VisitDeltaExpression(this);
        }
    }

    public sealed class FunctionCallSyntax : ExpressionSyntax
    {
        internal FunctionCallSyntax(
            Spanned<string> targetName,
            ImmutableArray<ExpressionSyntax> arguments)
        {
            TargetName = targetName;
            Arguments = arguments;
        }

        public Spanned<string> TargetName { get; }
        public ImmutableArray<ExpressionSyntax> Arguments { get; }

        public override SyntaxNodeKind Kind => SyntaxNodeKind.FunctionCall;

        public override void Accept(SyntaxVisitor visitor)
        {
            visitor.VisitFunctionCall(this);
        }

        public override TResult Accept<TResult>(SyntaxVisitor<TResult> visitor)
        {
            return visitor.VisitFunctionCall(this);
        }
    }
}