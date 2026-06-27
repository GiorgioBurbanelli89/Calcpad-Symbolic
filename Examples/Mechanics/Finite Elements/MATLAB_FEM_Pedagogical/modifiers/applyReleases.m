function K_reduced = applyReleases(K, releases)
% applyReleases - condensacion estatica de DOF liberados
% -------------------------------------------------------------------------
% Cuando un DOF esta liberado (rotula, deslizamiento) NO transmite fuerza
% por ese DOF -> hay que condensarlo estaticamente.
%
% Particion:
%    K = [ K_aa   K_ar ]   con r = released, a = active
%        [ K_ra   K_rr ]
%
% Condensacion:
%    K_reduced(a,a) = K_aa - K_ar * inv(K_rr) * K_ra
%
% El K resultante MANTIENE el mismo tama#o pero las filas/columnas
% de los DOF liberados quedan con 0 (o un valor pequeno para evitar
% singular).
%
% INPUT
%  K          matriz NxN
%  releases   vector logico Nx1 (true = DOF liberado, false = mantener)
%
% OUTPUT
%  K_reduced  matriz NxN condensada
% -------------------------------------------------------------------------

    if islogical(releases)
        rIdx = find(releases);
    else
        rIdx = find(releases ~= 0);
    end
    if isempty(rIdx)
        K_reduced = K;
        return;
    end

    n = size(K, 1);
    aIdx = setdiff(1:n, rIdx);

    Kaa = K(aIdx, aIdx);
    Kar = K(aIdx, rIdx);
    Kra = K(rIdx, aIdx);
    Krr = K(rIdx, rIdx);

    % Condensacion (Krr debe ser invertible)
    Kc = Kaa - Kar * (Krr \ Kra);

    K_reduced = zeros(n, n);
    K_reduced(aIdx, aIdx) = Kc;

    % Para evitar singular global, colocamos un epsilon en los DOF liberados.
    eps_val = 1e-12 * max(abs(diag(K)));
    for i = rIdx(:).'
        K_reduced(i, i) = eps_val;
    end
end
