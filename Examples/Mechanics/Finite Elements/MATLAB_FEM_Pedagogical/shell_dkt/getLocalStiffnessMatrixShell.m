function K = getLocalStiffnessMatrixShell(coords, mat)
% getLocalStiffnessMatrixShell - Shell triangular DKT/DST
% -------------------------------------------------------------------------
% Composicion clasica de Shell triangular plano:
%
%   K_shell = K_membrane (ANDES 9-DOF) + K_bending (DKT/DST 9-DOF)
%
% Cada nodo tiene 6 DOF: [ux uy uz thx thy thz]
% Total: 18 DOF.
%
% La parte membrana (ANDES) actua sobre [ux uy thz]_i (drilling).
% La parte bending (DKT) actua sobre [uz thx thy]_i.
%
% NOTAS PEDAGOGICAS
% -----------------
%  * DKT = Discrete Kirchhoff Triangle - placa delgada (sin corte).
%  * DST = Discrete Shear Triangle - placa moderada (Mindlin con corte).
%  * Para placas muy delgadas (L/t > 100) DKT y DST coinciden.
%  * Para placas gruesas (L/t < 10) hay que usar DST o un elemento Mindlin.
%
% Si mat.useDST = true, se anade la contribucion de corte K_shear via Bs.
% -------------------------------------------------------------------------

    CS = getCellSmoothingTerms(coords);
    A  = CS.area;

    % --- Bending (DKT / DST) --------------------------------------------
    % Usamos UN solo punto de Gauss en el centroide (xi=1/3, eta=1/3).
    % Para DKT esto es exacto porque B_b es lineal en xi/eta y D es cte.
    xi = 1/3;  eta = 1/3;
    Bb = getBendingStrainDisplacementMatrix(xi, eta, CS);

    if isfield(mat, 'Db') && ~isempty(mat.Db)
        Db = mat.Db;
    else
        Db = buildIsoDb(mat.E, mat.nu, mat.thickness);
    end

    Kb_local = Bb.' * Db * Bb * A;

    if isfield(mat, 'useDST') && mat.useDST
        Bs = getShearStrainDisplacementMatrix(CS);
        if isfield(mat, 'Ds') && ~isempty(mat.Ds)
            Ds = mat.Ds;
        else
            Ds = buildIsoDs(mat.E, mat.nu, mat.thickness);
        end
        Kb_local = Kb_local + Bs.' * Ds * Bs * A;
    end

    % --- Membrana (ANDES) ------------------------------------------------
    Km_local = getMembraneStiffnessMatrix(coords, mat);

    % --- Ensamblar 18x18 -------------------------------------------------
    % DOF locales por nodo: [ux uy uz thx thy thz]
    % - Membrana usa (ux,uy,thz)  -> indices 1,2,6
    % - Bending  usa (uz,thx,thy) -> indices 3,4,5
    K = zeros(18, 18);

    idxM_local = [1 2 6];                  % DOFs membrana por nodo
    idxB_local = [3 4 5];                  % DOFs bending  por nodo

    % indices globales en el vector 18
    membraneRows = [];   bendingRows = [];
    for n = 1:3
        offset = 6*(n-1);
        membraneRows = [membraneRows, offset + idxM_local]; %#ok<AGROW>
        bendingRows  = [bendingRows,  offset + idxB_local]; %#ok<AGROW>
    end

    K(membraneRows, membraneRows) = K(membraneRows, membraneRows) + Km_local;
    K(bendingRows,  bendingRows)  = K(bendingRows,  bendingRows)  + Kb_local;

    % --- Stabilizacion drilling (alpha pequeno) --------------------------
    % Evita rigidez nula en theta_z global. Felippa recomienda
    % alpha = 1e-4 * trace(Km).
    alpha = 1e-4 * trace(Km_local);
    for n = 1:3
        idxThZ = 6*(n-1) + 6;
        K(idxThZ, idxThZ) = K(idxThZ, idxThZ) + alpha;
    end
end
