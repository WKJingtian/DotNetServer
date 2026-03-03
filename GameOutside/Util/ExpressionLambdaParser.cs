using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;

namespace GameOutside.Util
{
    /// <summary>
    /// Builds delegates from math expressions. Primary entry supports COUNT (int) and DIFFICULTY_MULTIPLIER (double).
    /// </summary>
    public static class ExpressionLambdaParser
    {
        private sealed class Token
        {
            public string Text { get; }
            public TokenType Type { get; }
            public Token(string text, TokenType type)
            {
                Text = text;
                Type = type;
            }
        }

        private enum TokenType
        {
            Number,
            Identifier,
            Plus,
            Minus,
            Multiply,
            Divide,
            Power,
            LParen,
            RParen
        }

        private const string CountParamName = "Count";
        private const string DifficultyParamName = "DifficultyMult";

        /// <summary>
        /// Parses an expression string into a lambda that takes COUNT (int) and DIFFICULTY_MULTIPLIER (double) and returns int (via Convert.ToInt32).
        /// </summary>
        public static Func<double, double, double> EvaluateBattleScoreEquation(string stringToEval)
        {
            if (string.IsNullOrWhiteSpace(stringToEval))
                throw new ArgumentException("Expression is null or empty", nameof(stringToEval));

            var tokens = Tokenize(stringToEval);
            var rpn = ToRpn(tokens);
            var countParameter = Expression.Parameter(typeof(double), CountParamName);
            var difficultyParameter = Expression.Parameter(typeof(double), DifficultyParamName);
            var body = BuildExpression(rpn, countParameter, difficultyParameter);
            var lambda = Expression.Lambda<Func<double, double, double>>(body, countParameter, difficultyParameter);
            return lambda.Compile();
        }

        /// <summary>
        /// Parses an expression string into a lambda that evaluates to double.
        /// </summary>
        public static Func<double, double> EvaluateStringAsDouble(string stringToEval, string parameterName = "X")
        {
            if (string.IsNullOrWhiteSpace(stringToEval))
                throw new ArgumentException("Expression is null or empty", nameof(stringToEval));

            var tokens = Tokenize(stringToEval, parameterName);
            var rpn = ToRpn(tokens, parameterName);
            var parameter = Expression.Parameter(typeof(double), parameterName);
            var body = BuildExpression(rpn, parameter, parameterName);
            var lambda = Expression.Lambda<Func<double, double>>(body, parameter);
            return lambda.Compile();
        }

        /// <summary>
        /// Parses an expression string into a lambda that evaluates to int (rounded via Convert.ToInt32).
        /// </summary>
        public static Func<int, int> EvaluateStringAsInt(string stringToEval, string parameterName = "X")
        {
            var doubleLambda = EvaluateStringAsDouble(stringToEval, parameterName);
            return x => Convert.ToInt32(doubleLambda(x));
        }

        private static List<Token> Tokenize(string expr)
        {
            var tokens = new List<Token>();
            int i = 0;
            while (i < expr.Length)
            {
                char c = expr[i];
                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                if (char.IsDigit(c) || c == '.')
                {
                    int start = i;
                    while (i < expr.Length && (char.IsDigit(expr[i]) || expr[i] == '.'))
                    {
                        i++;
                    }
                    tokens.Add(new Token(expr.Substring(start, i - start), TokenType.Number));
                    continue;
                }

                if (char.IsLetter(c))
                {
                    int start = i;
                    while (i < expr.Length && char.IsLetterOrDigit(expr[i]))
                    {
                        i++;
                    }
                    var ident = expr.Substring(start, i - start);
                    tokens.Add(new Token(ident, TokenType.Identifier));
                    continue;
                }

                switch (c)
                {
                    case '+':
                        tokens.Add(new Token("+", TokenType.Plus));
                        break;
                    case '-':
                        tokens.Add(new Token("-", TokenType.Minus));
                        break;
                    case '*':
                        tokens.Add(new Token("*", TokenType.Multiply));
                        break;
                    case '/':
                        tokens.Add(new Token("/", TokenType.Divide));
                        break;
                    case '^':
                        tokens.Add(new Token("^", TokenType.Power));
                        break;
                    case '(':
                        tokens.Add(new Token("(", TokenType.LParen));
                        break;
                    case ')':
                        tokens.Add(new Token(")", TokenType.RParen));
                        break;
                    default:
                        throw new ArgumentException($"Unexpected character '{c}' in expression.");
                }
                i++;
            }
            return tokens;
        }

        private static List<Token> Tokenize(string expr, string parameterName)
        {
            // parameterName retained for compatibility; tokenization does not depend on it.
            return Tokenize(expr);
        }

        private static Queue<Token> ToRpn(List<Token> tokens)
        {
            var output = new Queue<Token>();
            var ops = new Stack<Token>();
            Token previous = null;

            foreach (var token in tokens)
            {
                switch (token.Type)
                {
                    case TokenType.Number:
                    case TokenType.Identifier:
                        output.Enqueue(token);
                        break;
                    case TokenType.Plus:
                    case TokenType.Minus:
                    case TokenType.Multiply:
                    case TokenType.Divide:
                    case TokenType.Power:
                    {
                        bool isUnaryMinus = token.Type == TokenType.Minus && (previous == null || previous.Type == TokenType.LParen || IsOperator(previous.Type));
                        var currentPrecedence = GetPrecedence(isUnaryMinus ? TokenType.Power : token.Type);
                        var currentRightAssoc = token.Type == TokenType.Power || isUnaryMinus;

                        while (ops.Count > 0 && ops.Peek().Type != TokenType.LParen &&
                               (GetPrecedence(ops.Peek().Type) > currentPrecedence ||
                               (GetPrecedence(ops.Peek().Type) == currentPrecedence && !currentRightAssoc)))
                        {
                            output.Enqueue(ops.Pop());
                        }

                        ops.Push(isUnaryMinus ? new Token("u-", TokenType.Power) : token);
                        break;
                    }
                    case TokenType.LParen:
                        ops.Push(token);
                        break;
                    case TokenType.RParen:
                        while (ops.Count > 0 && ops.Peek().Type != TokenType.LParen)
                        {
                            output.Enqueue(ops.Pop());
                        }
                        if (ops.Count == 0 || ops.Peek().Type != TokenType.LParen)
                            throw new ArgumentException("Mismatched parentheses in expression.");
                        ops.Pop();
                        break;
                }
                previous = token;
            }

            while (ops.Count > 0)
            {
                if (ops.Peek().Type == TokenType.LParen || ops.Peek().Type == TokenType.RParen)
                    throw new ArgumentException("Mismatched parentheses in expression.");
                output.Enqueue(ops.Pop());
            }
            return output;
        }

        private static Queue<Token> ToRpn(List<Token> tokens, string parameterName)
        {
            // parameterName retained for compatibility.
            return ToRpn(tokens);
        }

        private static Expression BuildExpression(Queue<Token> rpn, ParameterExpression countParameter, ParameterExpression difficultyParameter)
        {
            var stack = new Stack<Expression>();
            while (rpn.Count > 0)
            {
                var token = rpn.Dequeue();
                switch (token.Type)
                {
                    case TokenType.Number:
                        if (!double.TryParse(token.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var number))
                            throw new ArgumentException($"Invalid number literal '{token.Text}'");
                        stack.Push(Expression.Constant(number));
                        break;
                    case TokenType.Identifier:
                        if (string.Equals(token.Text, CountParamName, StringComparison.OrdinalIgnoreCase))
                        {
                            stack.Push(Expression.Convert(countParameter, typeof(double)));
                        }
                        else if (string.Equals(token.Text, DifficultyParamName, StringComparison.OrdinalIgnoreCase))
                        {
                            stack.Push(difficultyParameter);
                        }
                        else
                        {
                            throw new ArgumentException($"Unknown identifier '{token.Text}'");
                        }
                        break;
                    case TokenType.Plus:
                        EnsureOperands(stack, token.Text);
                        stack.Push(Expression.Add(stack.Pop(), stack.Pop()));
                        break;
                    case TokenType.Minus:
                        EnsureOperands(stack, token.Text);
                        var right = stack.Pop();
                        var left = stack.Pop();
                        stack.Push(Expression.Subtract(left, right));
                        break;
                    case TokenType.Multiply:
                        EnsureOperands(stack, token.Text);
                        stack.Push(Expression.Multiply(stack.Pop(), stack.Pop()));
                        break;
                    case TokenType.Divide:
                        EnsureOperands(stack, token.Text);
                        var divisor = stack.Pop();
                        var dividend = stack.Pop();
                        stack.Push(Expression.Divide(dividend, divisor));
                        break;
                    case TokenType.Power:
                        if (token.Text == "u-")
                        {
                            if (stack.Count < 1) throw new ArgumentException("Unary minus missing operand");
                            stack.Push(Expression.Negate(stack.Pop()));
                            break;
                        }
                        EnsureOperands(stack, token.Text);
                        var expRight = stack.Pop();
                        var expLeft = stack.Pop();
                        stack.Push(Expression.Call(typeof(Math).GetMethod(nameof(Math.Pow))!, expLeft, expRight));
                        break;
                }
            }

            if (stack.Count != 1)
                throw new ArgumentException("Invalid expression.");

            return stack.Pop();
        }

        private static Expression BuildExpression(Queue<Token> rpn, ParameterExpression parameter, string parameterName)
        {
            var stack = new Stack<Expression>();
            while (rpn.Count > 0)
            {
                var token = rpn.Dequeue();
                switch (token.Type)
                {
                    case TokenType.Number:
                        if (!double.TryParse(token.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var number))
                            throw new ArgumentException($"Invalid number literal '{token.Text}'");
                        stack.Push(Expression.Constant(number));
                        break;
                    case TokenType.Identifier:
                        if (string.Equals(token.Text, parameterName, StringComparison.OrdinalIgnoreCase))
                        {
                            stack.Push(parameter);
                        }
                        else
                        {
                            throw new ArgumentException($"Unknown identifier '{token.Text}'");
                        }
                        break;
                    case TokenType.Plus:
                        EnsureOperands(stack, token.Text);
                        stack.Push(Expression.Add(stack.Pop(), stack.Pop()));
                        break;
                    case TokenType.Minus:
                        EnsureOperands(stack, token.Text);
                        var right = stack.Pop();
                        var left = stack.Pop();
                        stack.Push(Expression.Subtract(left, right));
                        break;
                    case TokenType.Multiply:
                        EnsureOperands(stack, token.Text);
                        stack.Push(Expression.Multiply(stack.Pop(), stack.Pop()));
                        break;
                    case TokenType.Divide:
                        EnsureOperands(stack, token.Text);
                        var divisor = stack.Pop();
                        var dividend = stack.Pop();
                        stack.Push(Expression.Divide(dividend, divisor));
                        break;
                    case TokenType.Power:
                        if (token.Text == "u-")
                        {
                            if (stack.Count < 1) throw new ArgumentException("Unary minus missing operand");
                            stack.Push(Expression.Negate(stack.Pop()));
                            break;
                        }
                        EnsureOperands(stack, token.Text);
                        var expRight = stack.Pop();
                        var expLeft = stack.Pop();
                        stack.Push(Expression.Call(typeof(Math).GetMethod(nameof(Math.Pow))!, expLeft, expRight));
                        break;
                }
            }

            if (stack.Count != 1)
                throw new ArgumentException("Invalid expression.");

            return stack.Pop();
        }

        private static void EnsureOperands(Stack<Expression> stack, string op)
        {
            if (stack.Count < 2)
                throw new ArgumentException($"Operator '{op}' missing operand");
        }

        private static bool IsOperator(TokenType type)
        {
            return type == TokenType.Plus || type == TokenType.Minus || type == TokenType.Multiply || type == TokenType.Divide || type == TokenType.Power;
        }

        private static int GetPrecedence(TokenType type)
        {
            return type switch
            {
                TokenType.Power => 4,
                TokenType.Multiply => 3,
                TokenType.Divide => 3,
                TokenType.Plus => 2,
                TokenType.Minus => 2,
                _ => 0
            };
        }
    }
}

