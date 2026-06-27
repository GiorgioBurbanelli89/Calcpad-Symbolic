function K = getLocalStiffnessMatrix(elem, nodes, material)
% getLocalStiffnessMatrix
% -------------------------------------------------------------------------
% Dispatcher general: recibe un elemento y, segun cuantos nodos tenga,
% delega a la rutina correcta.
%
%  2 nodos -> Frame (viga Timoshenko 6 DOF/nodo, total 12)
%  3 nodos -> Shell DKT/DST triangular (6 DOF/nodo, total 18)
%  4 nodos -> Shell Q4 isoparametrico (6 DOF/nodo, total 24)
%            (si el material es "interface" -> Goodman 1968)
%
% INPUT
%  elem      struct con .type, .nodeIds, .releases, .partialFixity
%  nodes     matriz Nx3 con coordenadas globales [x y z]
%  material  struct con .E, .nu, .G, .rho, .thickness, .Db, .Ds, ...
%
% OUTPUT
%  K         matriz local del elemento en coordenadas locales
%            (sin transformar todavia al sistema global)
%
% Equivalente al `getLocalStiffnessMatrix(...)` del C++ original.
% -------------------------------------------------------------------------

    nNodes = numel(elem.nodeIds);
    coords = nodes(elem.nodeIds, :);   % filas = nodos del elemento

    switch nNodes
        case 2
            % --- Frame (barra 3D Timoshenko) ---------------------------
            K = getLocalStiffnessMatrixFrame(coords, material);

        case 3
            % --- Shell triangular DKT (flexion) + ANDES (membrana) -----
            K = getLocalStiffnessMatrixShell(coords, material);

        case 4
            if isfield(material, 'isInterface') && material.isInterface
                % --- Interface Goodman 1968 ---------------------------
                K = getLocalStiffnessMatrixInterface(coords, material);
            else
                % --- Shell Q4 isoparametrico --------------------------
                K = getLocalStiffnessMatrixShellQ4(coords, material);
            end

        otherwise
            error('Elemento con %d nodos no soportado', nNodes);
    end

    % Modificadores comunes (frame + shell pueden tenerlos)
    if isfield(elem, 'releases') && any(elem.releases)
        K = applyReleases(K, elem.releases);
    end
    if isfield(elem, 'partialFixity') && ~isempty(elem.partialFixity)
        K = applyPartialFixitySprings(K, elem.partialFixity);
    end
end
