function K = applyPartialFixitySprings(K, springs)
% applyPartialFixitySprings - resortes de fijacion parcial
% -------------------------------------------------------------------------
% Permite modelar conexiones SEMIRIGIDAS (no totalmente fijas, no totalmente
% liberadas). Se inserta un resorte en serie en cada DOF parcialmente fijo.
%
% Para un DOF i con factor "alpha" in (0, 1):
%   alpha = 0   -> rotula perfecta (release)
%   alpha = 1   -> empotramiento perfecto
%   alpha = 0.5 -> conexion 50% rigida
%
% La rigidez efectiva en ese DOF se reduce:
%   K_new(i,i) = K(i,i) * alpha / (1 - alpha + epsilon)
% (formula clasica de "semi-rigid connection" para vigas).
%
% INPUT
%  K       matriz original
%  springs vector Nx1 con factor alpha por DOF (NaN = sin cambio)
%
% OUTPUT
%  K modificada
% -------------------------------------------------------------------------

    n = size(K, 1);
    if length(springs) ~= n
        error('Vector springs debe tener longitud %d', n);
    end

    for i = 1:n
        a = springs(i);
        if isnan(a) || a >= 1 - 1e-12
            continue;             % sin cambio
        elseif a <= 1e-12
            % rotula completa - usar applyReleases en su lugar
            warning('alpha = 0: usa applyReleases para DOF %d', i);
            continue;
        end

        % Resorte equivalente
        k_orig = K(i, i);
        k_eff  = k_orig * a / (1 - a);

        % Reducir K(i,:) y K(:,i) proporcionalmente
        factor = k_eff / (k_eff + k_orig);
        K(i, :) = K(i, :) * factor;
        K(:, i) = K(:, i) * factor;
        K(i, i) = k_eff;
    end
end
