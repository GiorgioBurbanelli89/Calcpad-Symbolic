function Ds = buildIsoDs(E, nu, t, kappa)
% buildIsoDs
% -------------------------------------------------------------------------
% Matriz constitutiva de CORTE transversal (shear) para placa isotropa
% Mindlin-Reissner.
%
%  G = E / (2 (1 + nu))
%  Ds = kappa * G * t * I_2x2
%
% kappa (factor de correccion por corte):
%   - placa rectangular: 5/6
%   - placa con seccion variable: depende
%
% Para Kirchhoff (placa delgada) se IGNORA Ds (no hay deformacion por corte).
% -------------------------------------------------------------------------
    if nargin < 4 || isempty(kappa), kappa = 5/6; end
    G  = E / (2 * (1 + nu));
    Ds = kappa * G * t * eye(2);
end
