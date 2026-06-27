function Km = getMembraneStiffnessMatrix(coords, mat)
% getMembraneStiffnessMatrix  -  ANDES 9-DOF (membrana + drilling rotations)
% -------------------------------------------------------------------------
% Triangulo de 3 nodos, 3 DOF por nodo (ux, uy, theta_z).
% El "drilling rotation" theta_z es lo que diferencia al ANDES del CST clasico
% (que solo tiene 6 DOF de traslacion). Esto permite ensamblar elementos
% Shell SIN tener una rigidez nula en theta_z global.
%
% Referencia: Felippa & Militello (1992) - "Membrane triangles with
%   corner drilling freedoms II - The ANDES element"
%
% Estructura del DOF: { ux1, uy1, thz1, ux2, uy2, thz2, ux3, uy3, thz3 }
%
% Aqui hacemos la version "basica" (basic stiffness):
%   K_b = (1/V) * L * Dm * L^T
% donde L es la matriz "lumping" 9x3 y Dm la constitutiva de membrana.
% La parte "higher-order" (alpha-stabilization) se omite por claridad.
% -------------------------------------------------------------------------

    x1=coords(1,1); y1=coords(1,2);
    x2=coords(2,1); y2=coords(2,2);
    x3=coords(3,1); y3=coords(3,2);

    A2 = (x2-x1)*(y3-y1) - (x3-x1)*(y2-y1);   % 2*Area
    A  = 0.5 * A2;

    E  = mat.E;
    nu = mat.nu;
    t  = mat.thickness;

    % Constitutiva membrana plane stress
    Dm = (E / (1 - nu^2)) * [1   nu  0;
                              nu  1   0;
                              0   0   (1-nu)/2];

    % Matriz "L" (lumping) 9x3 que mapea fuerzas de borde a DOFs nodales.
    % Basada en la formulacion ANDES original.
    b1 = y2 - y3;  b2 = y3 - y1;  b3 = y1 - y2;
    c1 = x3 - x2;  c2 = x1 - x3;  c3 = x2 - x1;

    L = 0.5 * [ b1   0    c1;
                0    c1   b1;
                (b2-b3)/6   (c2-c3)/6   0;
                b2   0    c2;
                0    c2   b2;
                (b3-b1)/6   (c3-c1)/6   0;
                b3   0    c3;
                0    c3   b3;
                (b1-b2)/6   (c1-c2)/6   0 ];

    % K_basica
    Kb = (t / A) * L * Dm * L.';

    % Para didactica: solo basico (sin parte higher-order).
    Km = Kb;
end
