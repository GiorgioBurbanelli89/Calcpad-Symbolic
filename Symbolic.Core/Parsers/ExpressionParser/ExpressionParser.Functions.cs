using System;
using System.Collections.Generic;
using System.Linq;

namespace Calcpad.Core
{
    public partial class ExpressionParser
    {
        // Storage for user-defined #function blocks
        private sealed class UserFunction
        {
            internal string Name { get; }
            internal string[] Parameters { get; }
            internal List<string> BodyLines { get; }

            internal UserFunction(string name, string[] parameters, List<string> bodyLines)
            {
                Name = name;
                Parameters = parameters;
                BodyLines = bodyLines;
            }
        }

        private readonly Dictionary<string, UserFunction> _userFunctions = new(StringComparer.OrdinalIgnoreCase);
        private string _currentFunctionName;         // non-null when inside #function block
        private string[] _currentFunctionParams;
        private List<string> _currentFunctionBody;

        // Called when #function keyword is found
        private void ParseKeywordFunction(ReadOnlySpan<char> s)
        {
            // Parse: #function Name(param1; param2; param3)
            var text = s.ToString();
            var startIdx = text.IndexOf(' ');
            if (startIdx < 0)
            {
                AppendError("#function", "Missing function name", _currentLine);
                return;
            }
            var afterKeyword = text[(startIdx + 1)..].Trim();
            var parenIdx = afterKeyword.IndexOf('(');
            if (parenIdx < 0)
            {
                AppendError("#function", "Missing parameters: #function Name(param1; param2)", _currentLine);
                return;
            }
            _currentFunctionName = afterKeyword[..parenIdx].Trim();
            var closeIdx = afterKeyword.IndexOf(')');
            if (closeIdx < 0) closeIdx = afterKeyword.Length;
            var paramStr = afterKeyword[(parenIdx + 1)..closeIdx];
            _currentFunctionParams = paramStr.Split(';').Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
            _currentFunctionBody = [];
        }

        // Called when #end function keyword is found
        private void ParseKeywordEndFunction()
        {
            if (_currentFunctionName == null)
            {
                AppendError("#end function", "No matching #function", _currentLine);
                return;
            }
            var func = new UserFunction(_currentFunctionName, _currentFunctionParams, _currentFunctionBody);
            _userFunctions[_currentFunctionName] = func;
            _currentFunctionName = null;
            _currentFunctionParams = null;
            _currentFunctionBody = null;
        }

        // Check if we're inside a #function definition (lines should be accumulated, not executed)
        private bool IsInsideFunctionDefinition => _currentFunctionName != null;

        // Accumulate a line inside #function body
        private void AccumulateFunctionLine(string line)
        {
            _currentFunctionBody.Add(line);
        }

        // Check if a line contains a call to a user-defined function
        // Returns the function name if found, null otherwise
        private string DetectFunctionCall(ReadOnlySpan<char> line)
        {
            foreach (var kvp in _userFunctions)
            {
                var name = kvp.Key;
                var idx = line.IndexOf(name + "(");
                if (idx < 0) continue;
                // Check word boundary on the left
                if (idx > 0 && (char.IsLetterOrDigit(line[idx - 1]) || line[idx - 1] == '_'))
                    continue;
                return name;
            }
            return null;
        }

        // Execute a user-defined function call
        // line = "K = FrameKe(200000; 0.001; 3)" or "FrameKe(200000; 0.001; 3)"
        private void ExecuteFunctionCall(ReadOnlySpan<char> line, string funcName)
        {
            var func = _userFunctions[funcName];
            var lineStr = line.ToString();

            // Extract assignment variable (if any): "K = FrameKe(...)" → "K"
            string assignVar = null;
            var funcIdx = lineStr.IndexOf(funcName + "(", StringComparison.OrdinalIgnoreCase);
            if (funcIdx > 0)
            {
                var before = lineStr[..funcIdx].Trim();
                if (before.EndsWith("="))
                {
                    assignVar = before[..^1].Trim();
                }
            }

            // Extract arguments: "FrameKe(200000; 0.001; 3)" → ["200000", "0.001", "3"]
            var callStart = lineStr.IndexOf(funcName + "(", StringComparison.OrdinalIgnoreCase) + funcName.Length + 1;
            var callEnd = lineStr.LastIndexOf(')');
            if (callEnd < callStart) callEnd = lineStr.Length;
            var argsStr = lineStr[callStart..callEnd];
            // Split by ; but respect nested brackets
            var args = SplitArguments(argsStr);

            if (args.Length != func.Parameters.Length)
            {
                AppendError(lineStr, $"#function {funcName} expects {func.Parameters.Length} arguments, got {args.Length}", _currentLine);
                return;
            }

            // Collect all variables that will be modified: parameters + body assignments
            var savedVars = new Dictionary<string, IValue>();
            var newVars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Save parameter variables if they exist
            foreach (var param in func.Parameters)
            {
                var varRef = _parser.GetVariableRef(param);
                if (varRef != null && varRef.IsInitialized)
                    savedVars[param] = varRef.Value;
                else
                    newVars.Add(param);
            }

            // Detect body variable assignments and save their state
            foreach (var bodyLine in func.BodyLines)
            {
                var trimmed = bodyLine.Trim();
                var eqPos = trimmed.IndexOf('=');
                if (eqPos <= 0) continue;
                // Skip ==, <=, >=, !=
                if (eqPos + 1 < trimmed.Length && trimmed[eqPos + 1] == '=') continue;
                if (eqPos > 0 && (trimmed[eqPos - 1] == '<' || trimmed[eqPos - 1] == '>' || trimmed[eqPos - 1] == '!')) continue;
                var varName = trimmed[..eqPos].Trim();
                if (varName.Length == 0 || varName.Any(c => !char.IsLetterOrDigit(c) && c != '_')) continue;
                if (savedVars.ContainsKey(varName) || newVars.Contains(varName)) continue;
                var varRef = _parser.GetVariableRef(varName);
                if (varRef != null && varRef.IsInitialized)
                    savedVars[varName] = varRef.Value;
                else
                    newVars.Add(varName);
            }

            // Evaluate arguments and assign to parameter variables
            for (int i = 0; i < args.Length; i++)
            {
                var argExpr = args[i].Trim();
                _parser.Parse(argExpr);
                _parser.Calculate(false, -1);
                var argValue = _parser.ResultValue;
                _parser.SetVariable(func.Parameters[i], argValue);
            }

            // Execute body lines (hidden from output)
            var savedVisible = _isVisible;
            _isVisible = false;
            IValue returnValue = null;

            foreach (var bodyLine in func.BodyLines)
            {
                var trimmed = bodyLine.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                // Check if this is the return line: "FuncName = expr"
                bool isReturn = trimmed.StartsWith(func.Name, StringComparison.OrdinalIgnoreCase) &&
                    trimmed.Length > func.Name.Length && trimmed[func.Name.Length..].TrimStart().StartsWith("=");

                if (isReturn)
                {
                    // Extract the expression after "FuncName = "
                    var eqIdx = trimmed.IndexOf('=');
                    var returnExpr = trimmed[(eqIdx + 1)..].Trim();
                    _parser.Parse(returnExpr);
                    _parser.Calculate(false, -1);
                    returnValue = _parser.ResultValue;
                }
                else
                {
                    // Regular line — parse and calculate
                    _parser.Parse(trimmed);
                    _parser.Calculate(false, -1);
                }
            }

            _isVisible = savedVisible;

            // Assign return value to caller variable
            if (assignVar != null && returnValue != null)
            {
                _parser.SetVariable(assignVar, returnValue);
                // Show the result
                if (_isVisible)
                {
                    _parser.Parse($"{assignVar}");
                    _parser.Calculate(true, -1);
                    var html = _parser.ToHtml();
                    _sb.Append($"<span class=\"eq\">{html}</span>");
                }
            }

            // Restore saved variables (existed before the call)
            foreach (var kvp in savedVars)
                _parser.SetVariable(kvp.Key, kvp.Value);

            // Remove variables that were created inside the function (didn't exist before)
            foreach (var name in newVars)
                _parser.RemoveVariable(name);
        }

        // Split "a; b; c" respecting nested brackets
        private static string[] SplitArguments(string s)
        {
            var args = new List<string>();
            var depth = 0;
            var start = 0;
            for (int i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (c == '(' || c == '[') depth++;
                else if (c == ')' || c == ']') depth--;
                else if (c == ';' && depth == 0)
                {
                    args.Add(s[start..i]);
                    start = i + 1;
                }
            }
            args.Add(s[start..]);
            return args.ToArray();
        }
    }
}
