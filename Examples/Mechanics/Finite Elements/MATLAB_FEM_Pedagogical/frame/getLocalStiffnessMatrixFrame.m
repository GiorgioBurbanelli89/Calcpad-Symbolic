function K = getLocalStiffnessMatrixFrame(coords, mat)
% getLocalStiffnessMatrixFrame
% -------------------------------------------------------------------------
% Matriz de rigidez 12x12 de una barra 3D Timoshenko.
%
% DOF por nodo (6):  [u  v  w  rx  ry  rz]
% DOF totales: 12 -> [u1 v1 w1 rx1 ry1 rz1 | u2 v2 w2 rx2 ry2 rz2]
%
% Diferencia clave Euler-Bernoulli vs Timoshenko:
%   Timoshenko incluye la deformacion por CORTE (Phi_y, Phi_z != 0).
%   Cuando L >> h se recupera Euler-Bernoulli automaticamente.
%
% Formulas estandar (Bathe, "Finite Element Procedures", cap. 5).
% -------------------------------------------------------------------------

    p1 = coords(1, :);   p2 = coords(2, :);
    L  = norm(p2 - p1);

    E  = mat.E;
    G  = mat.G;
    A  = mat.A;
    Iy = mat.Iy;
    Iz = mat.Iz;
    J  = mat.J;                 % torsion (Saint-Venant)
    Asy = mat.Asy;              % area efectiva a corte en y (k_y * A)
    Asz = mat.Asz;              % area efectiva a corte en z (k_z * A)

    % --- Factores de cortante Phi -----------------------------------------
    % Phi_y = 12*E*Iz / (G*Asy*L^2)  (flexion en plano xy)
    % Phi_z = 12*E*Iy / (G*Asz*L^2)  (flexion en plano xz)
    if Asy > 0
        Phi_y = 12 * E * Iz / (G * Asy * L^2);
    else
        Phi_y = 0;   % Euler-Bernoulli
    end
    if Asz > 0
        Phi_z = 12 * E * Iy / (G * Asz * L^2);
    else
        Phi_z = 0;
    end

    % --- Subterminos repetidos --------------------------------------------
    EA_L = E * A / L;
    GJ_L = G * J / L;

    % flexion plano xy (involucra v y rz)
    fy = E * Iz / ((1 + Phi_y) * L^3);
    k1y = 12 * fy;
    k2y = 6 * L * fy;
    k3y = (4 + Phi_y) * L^2 * fy;
    k4y = (2 - Phi_y) * L^2 * fy;

    % flexion plano xz (involucra w y ry)  -- signos cambian
    fz = E * Iy / ((1 + Phi_z) * L^3);
    k1z = 12 * fz;
    k2z = 6 * L * fz;
    k3z = (4 + Phi_z) * L^2 * fz;
    k4z = (2 - Phi_z) * L^2 * fz;

    % --- Matriz K 12x12 (rellena por bloques) -----------------------------
    K = zeros(12, 12);

    % axial (u1, u2)
    K([1 7], [1 7]) = EA_L * [ 1 -1; -1  1];

    % torsion (rx1, rx2)
    K([4 10], [4 10]) = GJ_L * [ 1 -1; -1  1];

    % flexion plano xy: filas/cols (v1 rz1 v2 rz2) = (2 6 8 12)
    idxY = [2 6 8 12];
    Ky = [  k1y   k2y  -k1y   k2y;
            k2y   k3y  -k2y   k4y;
           -k1y  -k2y   k1y  -k2y;
            k2y   k4y  -k2y   k3y ];
    K(idxY, idxY) = K(idxY, idxY) + Ky;

    % flexion plano xz: filas/cols (w1 ry1 w2 ry2) = (3 5 9 11)
    %   ojo con signos: la rotacion ry esta acoplada con +w distinto al caso xy
    idxZ = [3 5 9 11];
    Kz = [  k1z  -k2z  -k1z  -k2z;
           -k2z   k3z   k2z   k4z;
           -k1z   k2z   k1z   k2z;
           -k2z   k4z   k2z   k3z ];
    K(idxZ, idxZ) = K(idxZ, idxZ) + Kz;
end
