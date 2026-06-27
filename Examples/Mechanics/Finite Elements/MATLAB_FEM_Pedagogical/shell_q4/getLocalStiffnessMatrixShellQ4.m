function K = getLocalStiffnessMatrixShellQ4(coords, mat)
% getLocalStiffnessMatrixShellQ4 - Shell cuadrilatero Q4 isoparametrico
% -------------------------------------------------------------------------
% Q4 4-nodos, 6 DOF por nodo (ux,uy,uz,thx,thy,thz) -> 24 DOF total.
%
% Composicion:
%   K_membrane (4x2 = 8 DOF ux,uy)  +  drilling fix
%   K_bending  (4x3 = 12 DOF uz, thx, thy) usando Kirchhoff o Mindlin
%
% Integracion: 2x2 puntos de Gauss en (xi, eta) in [-1,+1]^2.
% Puntos: +/- 1/sqrt(3),  pesos: 1.
%
% Este es el "Bathe Q4" clasico, MUY usado en losas y muros en ETABS/SAP/STAAD.
%
% PEDAGOGIA
% ---------
%   1. Para cada punto de Gauss (xi_g, eta_g):
%        - Calcular dN/dxi, dN/deta
%        - Construir Jacobiano J 2x2
%        - Calcular dN/dx, dN/dy = inv(J) * [dN/dxi; dN/deta]
%        - Construir B (3x8 membrana, 3x12 bending)
%        - Sumar B^T D B * det(J) * w
%
%   2. Ensamblar membrana y bending en K 24x24.
% -------------------------------------------------------------------------

    % --- Parametros ------------------------------------------------------
    E  = mat.E;
    nu = mat.nu;
    t  = mat.thickness;
    Dm = (E*t / (1 - nu^2)) * [1   nu  0;
                                nu  1   0;
                                0   0   (1-nu)/2];   % membrana plane stress
    Db = buildIsoDb(E, nu, t);                       % bending plate

    % --- Puntos de Gauss 2x2 ---------------------------------------------
    g = 1/sqrt(3);
    gp = [-g -g;  g -g;  g g;  -g g];   % 4 puntos
    w  = [ 1; 1; 1; 1 ];                % pesos

    % --- Acumuladores ----------------------------------------------------
    Km_local = zeros(8, 8);    % membrana: (ux,uy)_i, i=1..4
    Kb_local = zeros(12, 12);  % bending:  (uz,thx,thy)_i

    for k = 1:4
        xi  = gp(k, 1);
        eta = gp(k, 2);
        wg  = w(k);

        % Derivadas de N_i en (xi, eta), bilineales Q4
        % N_i = (1 +/- xi)(1 +/- eta)/4
        dN_dxi  = 0.25 * [ -(1-eta);  (1-eta);  (1+eta); -(1+eta) ];
        dN_deta = 0.25 * [ -(1-xi);  -(1+xi);   (1+xi);   (1-xi)  ];

        % Jacobiano 2x2:  J(i,j) = sum_n dN_n/d(natural_i) * coord(n, j)
        J = [ dN_dxi.'  * coords(:,1),  dN_dxi.'  * coords(:,2);
              dN_deta.' * coords(:,1),  dN_deta.' * coords(:,2) ];
        detJ = det(J);
        invJ = inv(J);

        % dN/dx, dN/dy
        dN_dx = invJ(1,1)*dN_dxi + invJ(1,2)*dN_deta;
        dN_dy = invJ(2,1)*dN_dxi + invJ(2,2)*dN_deta;

        % --- Bm membrana (3x8) ---
        Bm = zeros(3, 8);
        for n = 1:4
            Bm(:, 2*n-1:2*n) = [ dN_dx(n)   0;
                                 0          dN_dy(n);
                                 dN_dy(n)   dN_dx(n) ];
        end

        Km_local = Km_local + (Bm.' * Dm * Bm) * detJ * wg;

        % --- Bb bending (3x12) - Kirchhoff via DKQ no se hace asi, este Q4
        %     usa Mindlin (independent rotation theta_x, theta_y).
        Bb = zeros(3, 12);
        for n = 1:4
            % DOFs por nodo (uz, thx, thy) -> columnas 3n-2, 3n-1, 3n
            Bb(:, 3*n-2:3*n) = [ 0   0          -dN_dx(n);
                                 0   dN_dy(n)    0;
                                 0   dN_dx(n)   -dN_dy(n) ];
        end

        Kb_local = Kb_local + (Bb.' * Db * Bb) * detJ * wg;
    end

    % --- Ensamblar 24x24 -------------------------------------------------
    K = zeros(24, 24);
    idxM_local = [1 2];        % membrana DOFs por nodo (ux, uy)
    idxB_local = [3 4 5];      % bending  DOFs por nodo (uz, thx, thy)
    membraneRows = [];   bendingRows = [];
    for n = 1:4
        offset = 6*(n-1);
        membraneRows = [membraneRows, offset + idxM_local]; %#ok<AGROW>
        bendingRows  = [bendingRows,  offset + idxB_local]; %#ok<AGROW>
    end
    K(membraneRows, membraneRows) = K(membraneRows, membraneRows) + Km_local;
    K(bendingRows,  bendingRows)  = K(bendingRows,  bendingRows)  + Kb_local;

    % Drilling stabilization (thz)
    alpha = 1e-4 * trace(Km_local);
    for n = 1:4
        idxThZ = 6*(n-1) + 6;
        K(idxThZ, idxThZ) = K(idxThZ, idxThZ) + alpha;
    end
end
