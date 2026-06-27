function CS = getCellSmoothingTerms(coords)
% getCellSmoothingTerms
% -------------------------------------------------------------------------
% Devuelve los terminos geometricos usados por las B-matrices del DKT/DST:
%   - area del triangulo
%   - lados a, b, c y proyecciones
%   - cosenos directores de cada lado
%   - factores 1/(2A) que aparecen en las derivadas de N_i
%
% Para un triangulo con nodos (x1,y1),(x2,y2),(x3,y3) en plano local:
%
%    2A = (x2-x1)(y3-y1) - (x3-x1)(y2-y1)
%
% Salida: struct CS con campos:
%   .area              area del triangulo
%   .b1,.b2,.b3        y_jk = y_j - y_k        (constantes geometricas)
%   .c1,.c2,.c3        x_kj = x_k - x_j
%   .l12,.l23,.l31     longitudes de los lados
%   .cos_, .sin_       cosenos/senos de los lados respecto a x local
%
% Referencia: Batoz, Bathe, Ho (1980) - "A study of three-node triangular
% plate bending elements".
% -------------------------------------------------------------------------
    x1 = coords(1,1); y1 = coords(1,2);
    x2 = coords(2,1); y2 = coords(2,2);
    x3 = coords(3,1); y3 = coords(3,2);

    twoA = (x2 - x1) * (y3 - y1) - (x3 - x1) * (y2 - y1);
    CS.area = 0.5 * twoA;

    % constantes "y_jk", "x_kj" usadas en B-bending DKT
    CS.b1 = y2 - y3;   CS.b2 = y3 - y1;   CS.b3 = y1 - y2;
    CS.c1 = x3 - x2;   CS.c2 = x1 - x3;   CS.c3 = x2 - x1;

    % longitudes de los lados
    CS.l12 = sqrt((x2-x1)^2 + (y2-y1)^2);
    CS.l23 = sqrt((x3-x2)^2 + (y3-y2)^2);
    CS.l31 = sqrt((x1-x3)^2 + (y1-y3)^2);

    % senos/cosenos de cada lado (utiles para el DST/DKT mixto)
    CS.cos12 = (x2 - x1) / CS.l12;   CS.sin12 = (y2 - y1) / CS.l12;
    CS.cos23 = (x3 - x2) / CS.l23;   CS.sin23 = (y3 - y2) / CS.l23;
    CS.cos31 = (x1 - x3) / CS.l31;   CS.sin31 = (y1 - y3) / CS.l31;
end
