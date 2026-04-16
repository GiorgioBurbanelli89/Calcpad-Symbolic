using System;

namespace Calcpad.Core
{
    internal static partial class Lanczos
    {
        // Coefficients for Lanczos approximation
        internal static readonly double[] Coefficients = {
            0.99999999999999709182,
            57.156235665862923517,
            -59.597960355475491248,
            14.136097974741747174,
            -0.49191381609762019978,
            0.33994649984811888699e-4,
            0.46523628927048575665e-4,
            -0.98374475304879564677e-4,
            0.15808870322491248884e-3,
            -0.21026444172410488319e-3,
            0.21743961811521264320e-3,
            -0.16431810653676389022e-3,
            0.84418223983852743293e-4,
            -0.26190838401581408670e-4,
            0.36899182659531622704e-5
        };
        internal static readonly double G = 607.0 / 128.0;
    }
}
