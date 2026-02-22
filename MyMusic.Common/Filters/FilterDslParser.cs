using System.Globalization;
using System.Text;

namespace MyMusic.Common.Filters;

public class FilterDslParser
{
    private readonly string _input;
    private int _position;

    public FilterDslParser(string input)
    {
        _input = input.Trim();
        _position = 0;
    }

    private char CurrentChar => _input[_position];

    public static FilterRequest Parse(string dsl)
    {
        if (string.IsNullOrWhiteSpace(dsl))
        {
            return FilterRequest.Empty();
        }

        var parser = new FilterDslParser(dsl);
        return parser.ParseRoot();
    }

    private FilterRequest ParseRoot()
    {
        var (rules, combinator) = ParseExpressionListWithCombinator();
        return new FilterRequest
        {
            Combinator = combinator,
            Rules = rules,
        };
    }

    private (List<FilterRule> rules, FilterCombinator combinator) ParseExpressionListWithCombinator()
    {
        var rules = new List<FilterRule>();
        var lastCombinator = FilterCombinator.And;

        while (_position < _input.Length)
        {
            SkipWhitespace();
            if (_position >= _input.Length)
            {
                break;
            }

            var startPosition = _position;
            var rule = ParseExpression();
            if (rule != null)
            {
                rules.Add(rule);
            }
            else if (_position == startPosition)
            {
                throw new FormatException(
                    $"Unexpected character '{CurrentChar}' at position {_position}: {GetContext()}");
            }

            SkipWhitespace();

            if (_position >= _input.Length)
            {
                break;
            }

            var combinator = TryParseCombinator();
            if (combinator != null)
            {
                lastCombinator = combinator.Value;
            }
        }

        return (rules, lastCombinator);
    }

    private List<FilterRule> ParseExpressionList()
    {
        var (rules, _) = ParseExpressionListWithCombinator();
        return rules;
    }

    private FilterRule? ParseExpression()
    {
        SkipWhitespace();

        if (_position >= _input.Length)
        {
            return null;
        }

        if (CurrentChar == '(')
        {
            return ParseGroup();
        }

        if (CurrentChar == ')')
        {
            return null;
        }

        return ParseCondition();
    }

    private FilterGroupRule ParseGroup()
    {
        Expect('(');
        var rules = new List<FilterRule>();
        var combinator = FilterCombinator.And;

        while (_position < _input.Length && CurrentChar != ')')
        {
            SkipWhitespace();
            if (CurrentChar == ')')
            {
                break;
            }

            var startPosition = _position;
            var rule = ParseExpression();
            if (rule != null)
            {
                rules.Add(rule);
            }
            else if (_position == startPosition)
            {
                throw new FormatException(
                    $"Unexpected character '{CurrentChar}' at position {_position}: {GetContext()}");
            }

            SkipWhitespace();

            var nextCombinator = TryParseCombinator();
            if (nextCombinator != null)
            {
                combinator = nextCombinator.Value;
            }
        }

        Expect(')');

        return new FilterGroupRule
        {
            Combinator = combinator,
            Rules = rules,
        };
    }

    private FilterConditionRule? ParseCondition()
    {
        SkipWhitespace();

        var field = ParseField();
        if (string.IsNullOrEmpty(field))
        {
            if (_position < _input.Length && CurrentChar != ')')
            {
                throw new FormatException($"Expected field name at position {_position}: {GetContext()}");
            }

            return null;
        }

        SkipWhitespace();

        if (_position >= _input.Length)
        {
            throw new FormatException($"Expected operator after field '{field}' at position {_position}");
        }

        var op = ParseOperator();

        SkipWhitespace();

        var (value, value2) = ParseValue(op);

        return new FilterConditionRule
        {
            Field = field,
            Operator = op,
            Value = value,
            Value2 = value2,
        };
    }

    private string ParseField()
    {
        var start = _position;

        while (_position < _input.Length)
        {
            if (char.IsLetterOrDigit(CurrentChar) || CurrentChar == '_' || CurrentChar == '.')
            {
                _position++;
            }
            else if (CurrentChar == '[')
            {
                var bracketStart = _position;
                _position++;
                SkipWhitespace();

                if (TryMatchKeyword("any") || TryMatchKeyword("all"))
                {
                    SkipWhitespace();
                    if (_position >= _input.Length || CurrentChar != ']')
                    {
                        throw new FormatException(
                            $"Expected ']' after quantifier at position {_position}: {GetContext()}");
                    }

                    _position++;
                }
                else
                {
                    throw new FormatException(
                        $"Expected 'any' or 'all' in quantifier at position {_position}: {GetContext()}");
                }
            }
            else
            {
                break;
            }
        }

        return _input[start.._position];
    }

    private FilterOperator ParseOperator()
    {
        if (TryMatch("==") || TryMatch("="))
        {
            return FilterOperator.Eq;
        }

        if (TryMatch("!=") || TryMatch("<>"))
        {
            return FilterOperator.Neq;
        }

        if (TryMatch(">="))
        {
            return FilterOperator.Gte;
        }

        if (TryMatch("<="))
        {
            return FilterOperator.Lte;
        }

        if (TryMatch(">"))
        {
            return FilterOperator.Gt;
        }

        if (TryMatch("<"))
        {
            return FilterOperator.Lt;
        }

        if (TryMatch("~"))
        {
            return FilterOperator.Contains;
        }

        if (TryMatchKeyword("contains"))
        {
            return FilterOperator.Contains;
        }

        if (TryMatchKeyword("startsWith"))
        {
            return FilterOperator.StartsWith;
        }

        if (TryMatchKeyword("endsWith"))
        {
            return FilterOperator.EndsWith;
        }

        if (TryMatchKeyword("in"))
        {
            return FilterOperator.In;
        }

        if (TryMatchKeyword("notIn"))
        {
            return FilterOperator.NotIn;
        }

        if (TryMatchKeyword("isNull"))
        {
            return FilterOperator.IsNull;
        }

        if (TryMatchKeyword("isNotNull"))
        {
            return FilterOperator.IsNotNull;
        }

        if (TryMatchKeyword("between"))
        {
            return FilterOperator.Between;
        }

        if (TryMatchKeyword("isTrue"))
        {
            return FilterOperator.IsTrue;
        }

        if (TryMatchKeyword("isFalse"))
        {
            return FilterOperator.IsFalse;
        }

        throw new FormatException($"Unknown operator at position {_position}: {GetContext()}");
    }

    private bool TryMatch(string op)
    {
        if (_position + op.Length <= _input.Length &&
            _input.Substring(_position, op.Length) == op)
        {
            _position += op.Length;
            return true;
        }

        return false;
    }

    private bool TryMatchKeyword(string keyword)
    {
        if (_position + keyword.Length <= _input.Length &&
            _input.Substring(_position, keyword.Length).Equals(keyword, StringComparison.OrdinalIgnoreCase))
        {
            var nextPos = _position + keyword.Length;
            if (nextPos >= _input.Length || !char.IsLetterOrDigit(_input[nextPos]))
            {
                _position = nextPos;
                return true;
            }
        }

        return false;
    }

    private (object? value, object? value2) ParseValue(FilterOperator op)
    {
        SkipWhitespace();

        if (op == FilterOperator.IsNull || op == FilterOperator.IsNotNull ||
            op == FilterOperator.IsTrue || op == FilterOperator.IsFalse)
        {
            return (null, null);
        }

        if (op == FilterOperator.Between)
        {
            var value1 = ParseSingleValue();
            SkipWhitespace();
            ExpectKeyword("and");
            SkipWhitespace();
            var value2 = ParseSingleValue();
            return (value1, value2);
        }

        if (op == FilterOperator.In || op == FilterOperator.NotIn)
        {
            return (ParseArrayValue(), null);
        }

        return (ParseSingleValue(), null);
    }

    private object? ParseSingleValue()
    {
        SkipWhitespace();

        if (_position >= _input.Length)
        {
            throw new FormatException($"Expected value at position {_position}");
        }

        if (CurrentChar == '"' || CurrentChar == '\'')
        {
            return ParseQuotedString();
        }

        if (TryMatchKeyword("true"))
        {
            return true;
        }

        if (TryMatchKeyword("false"))
        {
            return false;
        }

        if (TryMatchKeyword("null"))
        {
            return null;
        }

        return ParseNumberOrUnquotedString();
    }

    private string ParseQuotedString()
    {
        var quote = CurrentChar;
        _position++;

        var sb = new StringBuilder();
        while (_position < _input.Length && CurrentChar != quote)
        {
            if (CurrentChar == '\\' && _position + 1 < _input.Length)
            {
                _position++;
                sb.Append(CurrentChar switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    _ => CurrentChar,
                });
            }
            else
            {
                sb.Append(CurrentChar);
            }

            _position++;
        }

        if (_position >= _input.Length)
        {
            throw new FormatException($"Unterminated string at position {_position}");
        }

        _position++;
        return sb.ToString();
    }

    private object ParseNumberOrUnquotedString()
    {
        var start = _position;

        while (_position < _input.Length &&
               (char.IsDigit(CurrentChar) || CurrentChar == '.' ||
                CurrentChar == '-' || CurrentChar == '+' || CurrentChar == 'e' || CurrentChar == 'E'))
        {
            _position++;
        }

        var value = _input[start.._position];

        if (string.IsNullOrEmpty(value))
        {
            throw new FormatException($"Expected value at position {_position}: {GetContext()}");
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
        {
            if (num == Math.Truncate(num) && num is >= int.MinValue and <= int.MaxValue)
            {
                return (int)num;
            }

            return num;
        }

        return value;
    }

    private List<object> ParseArrayValue()
    {
        Expect('[');
        var values = new List<object>();

        while (_position < _input.Length && CurrentChar != ']')
        {
            SkipWhitespace();
            if (CurrentChar == ']')
            {
                break;
            }

            if (CurrentChar == ',')
            {
                _position++;
                SkipWhitespace();
                continue;
            }

            var startPosition = _position;
            var value = ParseSingleValue();
            if (value != null)
            {
                values.Add(value);
            }
            else if (_position == startPosition)
            {
                throw new FormatException(
                    $"Unexpected character '{CurrentChar}' in array at position {_position}: {GetContext()}");
            }
        }

        Expect(']');
        return values;
    }

    private FilterCombinator? TryParseCombinator()
    {
        SkipWhitespace();

        if (TryMatchKeyword("and"))
        {
            return FilterCombinator.And;
        }

        if (TryMatchKeyword("or"))
        {
            return FilterCombinator.Or;
        }

        return null;
    }

    private void SkipWhitespace()
    {
        while (_position < _input.Length && char.IsWhiteSpace(CurrentChar))
        {
            _position++;
        }
    }

    private void Expect(char c)
    {
        SkipWhitespace();
        if (_position >= _input.Length || CurrentChar != c)
        {
            throw new FormatException($"Expected '{c}' at position {_position}: {GetContext()}");
        }

        _position++;
    }

    private void ExpectKeyword(string keyword)
    {
        if (!TryMatchKeyword(keyword))
        {
            throw new FormatException($"Expected '{keyword}' at position {_position}: {GetContext()}");
        }
    }

    private string GetContext()
    {
        var start = Math.Max(0, _position - 10);
        var end = Math.Min(_input.Length, _position + 10);
        return $"...{_input[start..end]}...";
    }
}