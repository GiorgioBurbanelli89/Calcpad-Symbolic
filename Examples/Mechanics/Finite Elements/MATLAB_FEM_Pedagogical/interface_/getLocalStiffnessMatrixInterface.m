function K = getLocalStiffnessMatrixInterface(coords, mat)
% getLocalStiffnessMatrixInterface  -  Elemento de interface Goodman 1968
% -------------------------------------------------------------------------
% Elemento Q4 "zero-thickness" para representar discontinuidades:
%   - interface losa-suelo (Winkler distribuido)
%   - junta entre dos solidos (suelo-roca, suelo-muro)
%   - despegue zapata-suelo
%
% Goodman, Taylor & Brekke (1968) - "A model for the mechanics of jointed rock"
%
% El elemento tiene 4 nodos: 2 sobre la superficie superior (1,2) y 2 sobre
% la inferior (3,4). DOF por nodo: solo traslaciones (ux, uy, uz) = 3.
%
% Total DOF: 12. La rigidez es:
%
%    K = integral B^T D B dA
%
% donde:
%    D = diag(kn, ks_x, ks_y)   con kn = rigidez normal, ks = corte
%    B  = matriz que da el "salto" relativo top-bottom
%
% Tipico:
%   kn = E_subgrade / h   (h espesor representativo)
%   ks = G_subgrade / h
% -------------------------------------------------------------------------

    % Material: rigideces por unidad de area (no por volumen)
    kn = mat.kn;          % normal
    ksx = mat.ks;         % corte x
    ksy = mat.ks;         % corte y
    D   = diag([kn, ksx, ksy]);

    % Area "media" del Q4 (usamos la geometria de la cara superior solamente).
    % Los nodos 1,2,3,4 estan en (top1, top2, bot2, bot1) idealmente coincidentes.
    % Asumimos coords(1:2,:) = top, coords(3:4,:) = bottom.
    topCoords = coords(1:2, :);
    % Para una linea (2 nodos en 2D) el "area por unidad de profundidad" es L.
    L = norm(topCoords(2,:) - topCoords(1,:));

    % Integracion 2 puntos de Gauss a lo largo del lado
    g = 1/sqrt(3);
    gp = [-g; g];

    K = zeros(12, 12);

    for k = 1:2
        xi = gp(k);
        % N1 = (1-xi)/2,  N2 = (1+xi)/2
        N1 = 0.5*(1 - xi);
        N2 = 0.5*(1 + xi);

        % Matriz B: salto entre top y bottom interpolado por N1, N2
        % DOFs: [u1 v1 w1   u2 v2 w2   u3 v3 w3   u4 v4 w4]
        % Salto = top - bottom = (N1*u1 + N2*u2) - (N1*u4 + N2*u3)
        %       ojo orden: nodo3 bajo nodo2, nodo4 bajo nodo1 (convencion)
        I3 = eye(3);
        B  = [ N1*I3, N2*I3, -N2*I3, -N1*I3 ];

        % det(J) en 1D = L/2
        K = K + B.' * D * B * (L/2);
    end
end
