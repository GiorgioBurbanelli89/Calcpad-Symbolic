function Db = buildIsoDb(E, nu, t)
% buildIsoDb
% -------------------------------------------------------------------------
% Matriz constitutiva de FLEXION (bending) para placa isotropa.
%
%  D = E*t^3 / (12 (1 - nu^2))
%  Db = D * [ 1   nu  0
%             nu  1   0
%             0   0   (1-nu)/2 ]
%
% Asume placa delgada (Kirchhoff) o Mindlin (en cuyo caso Ds aparte).
% -------------------------------------------------------------------------
    D = E * t^3 / (12 * (1 - nu^2));
    Db = D * [ 1   nu   0;
               nu  1    0;
               0   0    (1 - nu) / 2 ];
end
