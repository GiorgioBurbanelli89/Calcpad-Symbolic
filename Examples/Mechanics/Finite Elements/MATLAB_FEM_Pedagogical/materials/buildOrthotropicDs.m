function Ds = buildOrthotropicDs(G13, G23, t, kappa)
% buildOrthotropicDs
% -------------------------------------------------------------------------
% Matriz de corte transversal para placa ortotropa Mindlin-Reissner.
%
%  Ds = kappa * t * diag(G13, G23)
%
% G13 = corte fuera-de-plano en direccion 1
% G23 = corte fuera-de-plano en direccion 2
% -------------------------------------------------------------------------
    if nargin < 4 || isempty(kappa), kappa = 5/6; end
    Ds = kappa * t * diag([G13, G23]);
end
