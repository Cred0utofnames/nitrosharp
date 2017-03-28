﻿using System;

namespace SciAdvNet.NSScript.Execution
{
    public sealed class RecursiveExpressionEvaluator
    {
        private readonly EvaluatingVisitor _evalVisitor;

        public RecursiveExpressionEvaluator()
        {
            _evalVisitor = new EvaluatingVisitor();
        }

        public ConstantValue EvaluateExpression(Expression expression, Frame frame)
        {
            _evalVisitor.Frame = frame;
            return _evalVisitor.Visit(expression);
        }
    }

    internal sealed class EvaluatingVisitor : SyntaxVisitor<ConstantValue>
    {
        public Frame Frame { get; set; }

        public override ConstantValue VisitLiteral(Literal literal)
        {
            return literal.Value;
        }

        public override ConstantValue VisitVariable(Variable variable)
        {
            return Frame.Globals[variable.Name.SimplifiedName];
        }

        public override ConstantValue VisitNamedConstant(NamedConstant namedConstant)
        {
            return new ConstantValue(namedConstant.Name.FullName);
        }

        public override ConstantValue VisitUnaryExpression(UnaryExpression unaryExpression)
        {
            return ApplyUnaryOperation(unaryExpression.Operand, unaryExpression.OperationKind);
        }

        public override ConstantValue VisitAssignmentExpression(AssignmentExpression assignmentExpression)
        {
            string targetName = assignmentExpression.Target.Name.SimplifiedName;
            var value = Visit(assignmentExpression.Value);
            Frame.Globals[targetName] = value;
            return value;
        }

        public override ConstantValue VisitBinaryExpression(BinaryExpression binaryExpression)
        {
            var leftValue = Visit(binaryExpression.Left);
            var rightValue = Visit(binaryExpression.Right);

            return ApplyBinaryOperation(leftValue, binaryExpression.OperationKind, rightValue);
        }

        public override ConstantValue VisitConstantValue(ConstantValue constantValue)
        {
            return base.VisitConstantValue(constantValue);
        }

        public override ConstantValue VisitParameterReference(ParameterReference parameterReference)
        {
            return Frame.Arguments[parameterReference.ParameterName.SimplifiedName];
        }

        private static ConstantValue ApplyBinaryOperation(ConstantValue leftOperand, OperationKind operationKind, ConstantValue rightOperand)
        {
            switch (operationKind)
            {
                case OperationKind.Addition:
                    return leftOperand + rightOperand;
                case OperationKind.Subtraction:
                    return leftOperand - rightOperand;
                case OperationKind.Multiplication:
                    return leftOperand * rightOperand;
                case OperationKind.Division:
                    return leftOperand / rightOperand;
                case OperationKind.Equal:
                    return leftOperand == rightOperand;
                case OperationKind.NotEqual:
                    return leftOperand != rightOperand;
                case OperationKind.LessThan:
                    return leftOperand < rightOperand;
                case OperationKind.LessThanOrEqual:
                    return leftOperand <= rightOperand;
                case OperationKind.GreaterThan:
                    return leftOperand > rightOperand;
                case OperationKind.GreaterThanOrEqual:
                    return leftOperand >= rightOperand;
                case OperationKind.LogicalAnd:
                    return leftOperand && rightOperand;
                case OperationKind.LogicalOr:
                default:
                    return leftOperand || rightOperand;
            }
        }

        private ConstantValue ApplyUnaryOperation(Expression operand, OperationKind operationKind)
        {
            if (operationKind == OperationKind.LogicalNegation)
            {
                return !Visit(operand);
            }

            if (operand.Kind != SyntaxNodeKind.Variable &&
                (operationKind == OperationKind.PostfixIncrement || operationKind == OperationKind.PostfixIncrement))
            {
                string op = OperationInfo.GetText(operationKind);
                throw new InvalidOperationException($"Unary operator '{op}' can only be applied to variables.");
            }

            ConstantValue oldValue;

            string variableName = string.Empty;
            if (operand.Kind == SyntaxNodeKind.Variable)
            {
                variableName = (operand as Variable).Name.FullName;
                oldValue = Frame.Globals[((operand as Variable).Name.FullName)];
            }
            else
            {
                oldValue = Visit(operand);
            }

            switch (operationKind)
            {
                case OperationKind.UnaryPlus:
                    return oldValue;

                case OperationKind.UnaryMinus:
                    return -oldValue;

                case OperationKind.PostfixIncrement:
                    Frame.Globals[variableName] = oldValue++;
                    return oldValue;

                case OperationKind.PostfixDecrement:
                default:
                    Frame.Globals[variableName] = oldValue--;
                    return oldValue;
            }
        }
    }
}