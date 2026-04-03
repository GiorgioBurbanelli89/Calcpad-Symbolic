// Quick test of AngouriMath
using AngouriMath;
using static AngouriMath.MathS;

// Symbolic differentiation
var x = Var("x");
var expr = x * x + 3 * x + 1;
var deriv = expr.Differentiate(x);
Console.WriteLine($"f(x) = {expr}");
Console.WriteLine($"f'(x) = {deriv.Simplify()}");

// Integration
var integral = expr.Integrate(x);
Console.WriteLine($"∫f(x)dx = {integral.Simplify()}");

// Matrix symbolic
var a = Var("a");
var b = Var("b");
var expr2 = (a + b).Expand();
Console.WriteLine($"(a+b) expanded = {expr2}");

// Solve equation
var eq = (x * x - 4).SolveEquation(x);
Console.WriteLine($"x²-4=0 → x = {eq}");
