using System;
using Calcpad.Core;

var parser = new ExpressionParser();

// Test 1: Simple $Draw
Console.WriteLine("=== Test 1: Simple Draw ===");
var code1 = "L = 6\r\nh = 0.4\r\n" + "$" + "Draw{line 0 0 L 0|line 0 0 0 h}";
try {
    parser.Parse(code1);
    Console.WriteLine(parser.HtmlResult);
} catch (Exception ex) {
    Console.WriteLine("ERROR: " + ex.Message);
}

// Test 2: Literal values only
Console.WriteLine("\n=== Test 2: Literal Draw ===");
parser = new ExpressionParser();
var code2 = "$" + "Draw{line 0 0 6 0|line 0 0 0 3}";
try {
    parser.Parse(code2);
    Console.WriteLine(parser.HtmlResult);
} catch (Exception ex) {
    Console.WriteLine("ERROR: " + ex.Message);
}
