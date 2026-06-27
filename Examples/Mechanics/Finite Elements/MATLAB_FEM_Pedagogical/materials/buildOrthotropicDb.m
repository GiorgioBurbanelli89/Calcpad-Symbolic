function Db = buildOrthotropicDb(E1, E2, nu12, G12, t)
% buildOrthotropicDb
% -------------------------------------------------------------------------
% Matriz constitutiva de flexion para placa ORTOTROPA (laminados, madera,
% hormigon armado en direcciones x/y, etc).
%
% Reciprocidad de Maxwell:  nu21 = nu12 * E2 / E1
%
%        | E1   nu12*E2  0   |   t^3
%   Cb = | nu12*E2  E2    0  | * --
%        | 0    0     G12*(1-...) |  12
%
% Implementacion clasica (Reddy, "Mechanics of Laminated Composite Plates").
% -------------------------------------------------------------------------
    nu21 = nu12 * E2 / E1;
    denom = 1 - nu12 * nu21;

    Q11 = E1 / denom;
    Q22 = E2 / denom;
    Q12 = nu12 * E2 / denom;
    Q66 = G12;

    factor = t^3 / 12;
    Db = factor * [ Q11   Q12   0;
                    Q12   Q22   0;
                    0     0     Q66 ];
end
