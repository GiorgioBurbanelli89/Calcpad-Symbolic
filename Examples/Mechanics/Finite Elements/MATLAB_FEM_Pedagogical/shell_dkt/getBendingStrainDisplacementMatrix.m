function Bb = getBendingStrainDisplacementMatrix(xi, eta, CS)
% getBendingStrainDisplacementMatrix
% -------------------------------------------------------------------------
% Matriz B_b 3x9 que relaciona los giros nodales con las curvaturas en
% un punto (xi, eta) del triangulo DKT.
%
%   {kappa_x; kappa_y; kappa_xy} = B_b * {w1, thx1, thy1, w2, thx2, thy2,
%                                          w3, thx3, thy3}
%
% El DKT impone que las funciones de forma C^1 cumplen Kirchhoff a lo largo
% de los lados (de ahi "Discrete Kirchhoff Triangle"). Las curvaturas se
% derivan de las funciones cuadraticas de los giros.
%
% Aqui presentamos la version CLASICA (Batoz 1980) escrita en coordenadas
% naturales (xi, eta) del triangulo: lambda1 = 1-xi-eta, lambda2=xi, lambda3=eta.
%
% Por brevedad pedagogica usamos la formulacion directa con las "p_k", "q_k",
% "r_k", "t_k" del paper. En produccion (C++) esto esta cacheado.
% -------------------------------------------------------------------------

    A   = CS.area;
    b1=CS.b1; b2=CS.b2; b3=CS.b3;
    c1=CS.c1; c2=CS.c2; c3=CS.c3;

    % coeficientes de Batoz (lados ij)
    l4 = b3^2 + c3^2;       % lado 12
    l5 = b1^2 + c1^2;       % lado 23
    l6 = b2^2 + c2^2;       % lado 31

    p4 = -6*c3/l4;          p5 = -6*c1/l5;          p6 = -6*c2/l6;
    q4 =  3*b3*c3/l4;       q5 =  3*b1*c1/l5;       q6 =  3*b2*c2/l6;
    r4 =  3*c3^2/l4;        r5 =  3*c1^2/l5;        r6 =  3*c2^2/l6;
    t4 = -6*b3/l4;          t5 = -6*b1/l5;          t6 = -6*b2/l6;

    L1 = 1 - xi - eta;   L2 = xi;   L3 = eta;
    %#ok<NASGU>  -- las dejamos por claridad

    % Funciones de forma cuadraticas y sus derivadas en xi/eta
    % (consulta Batoz 1980 tabla I para la forma exacta).
    % Para no inflar el archivo a 400 lineas reproducimos las derivadas
    % "Hx,xi" "Hy,xi" "Hx,eta" "Hy,eta" como vectores 9x1.

    Hx_xi  = [ p6*(1-2*xi) + (p5-p6)*eta;
               q6*(1-2*xi) - (q5+q6)*eta;
               -4 + 6*(xi+eta) + r6*(1-2*xi) - eta*(r5+r6);
               -p6*(1-2*xi) + eta*(p4+p6);
               q6*(1-2*xi) - eta*(q6-q4);
               -2 + 6*xi + r6*(1-2*xi) + eta*(r4-r6);
               -eta*(p5+p4);
                eta*(q4-q5);
               -eta*(r5-r4) ];

    Hy_xi  = [ t6*(1-2*xi) + (t5-t6)*eta;
               1 + r6*(1-2*xi) - (r5+r6)*eta;
              -q6*(1-2*xi) + eta*(q5+q6);
              -t6*(1-2*xi) + eta*(t4+t6);
              -1 + r6*(1-2*xi) + eta*(r4-r6);
              -q6*(1-2*xi) - eta*(q4-q6);
              -eta*(t4+t5);
                eta*(r4-r5);
              -eta*(q4-q5) ];

    Hx_eta = [-p5*(1-2*eta) - xi*(p6-p5);
               q5*(1-2*eta) - xi*(q5+q6);
              -4 + 6*(xi+eta) + r5*(1-2*eta) - xi*(r5+r6);
               xi*(p4+p6);
               xi*(q4-q6);
              -xi*(r6-r4);
               p5*(1-2*eta) - xi*(p4+p5);
               q5*(1-2*eta) + xi*(q4-q5);
              -2 + 6*eta + r5*(1-2*eta) + xi*(r4-r5) ];

    Hy_eta = [-t5*(1-2*eta) - xi*(t6-t5);
               1 + r5*(1-2*eta) - xi*(r5+r6);
              -q5*(1-2*eta) + xi*(q5+q6);
               xi*(t4+t6);
               xi*(r4-r6);
              -xi*(q4-q6);
               t5*(1-2*eta) - xi*(t4+t5);
              -1 + r5*(1-2*eta) + xi*(r4-r5);
              -q5*(1-2*eta) - xi*(q4-q5) ];

    % B_b en (xi, eta) - se transforma con la inversa del jacobiano triangular
    % d/dx = (b1*d/dL1 + b2*d/dL2 + b3*d/dL3) / (2A)  pero al usar xi-eta
    % directamente:
    %   d/dx = (b2*d/dxi + b3*d/deta) / (2A)
    %   d/dy = (c2*d/dxi + c3*d/deta) / (2A)
    inv2A = 1 / (2 * A);

    Bb = zeros(3, 9);
    Bb(1, :) = inv2A * ( b2 * Hx_xi.' + b3 * Hx_eta.' );           % kappa_x
    Bb(2, :) = inv2A * ( c2 * Hy_xi.' + c3 * Hy_eta.' );           % kappa_y
    Bb(3, :) = inv2A * ( c2 * Hx_xi.' + c3 * Hx_eta.' ...
                       + b2 * Hy_xi.' + b3 * Hy_eta.' );           % kappa_xy
end
