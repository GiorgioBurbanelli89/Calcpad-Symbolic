% =========================================================================
% main_demo.m  -  Script demostrador del codigo FEM en MATLAB
% =========================================================================
% Recorre los 4 tipos de elementos (Frame, Shell DKT, Q4, Interface) y
% muestra la matriz de rigidez local de cada uno.
%
% Equivalente al tour del C++ getLocalStiffnessMatrix().
% =========================================================================

clear; clc; close all;

% Agregar todas las carpetas al path
addpath('dispatch', 'frame', 'shell_dkt', 'shell_q4', 'interface_', ...
        'materials', 'modifiers');

% Material hormigon (kN, m)
E_c  = 3.5e7;          % kN/m^2  (= 35 GPa)
nu_c = 0.20;
G_c  = E_c / (2*(1+nu_c));
rho_c = 2.5;           % t/m^3

fprintf('\n==============================================================\n');
fprintf('  Demo FEM - matrices de rigidez local de cada elemento\n');
fprintf('==============================================================\n\n');

% =========================================================================
% (1) FRAME viga Timoshenko 6 m, seccion 0.30 x 0.50
% =========================================================================
fprintf('--- (1) Frame Timoshenko ---\n');

coordsFrame = [0 0 0;
               6 0 0];

bx = 0.30;  by = 0.50;
matFrame = struct( ...
    'E',  E_c, ...
    'G',  G_c, ...
    'A',  bx*by, ...
    'Iy', bx*by^3/12, ...
    'Iz', by*bx^3/12, ...
    'J',  bx*by*(bx^2+by^2)/12, ...
    'Asy', 5/6 * bx*by, ...
    'Asz', 5/6 * bx*by);

elemFrame = struct('type', 'frame', 'nodeIds', [1 2]);
nodes = coordsFrame;

K_frame = getLocalStiffnessMatrix(elemFrame, nodes, matFrame);
fprintf('  K_frame size: %dx%d  (esperado 12x12)\n', size(K_frame,1), size(K_frame,2));
fprintf('  det(K_frame) (sin restraints) = %g  (debe ser cero -> 6 modos rigidos)\n', det(K_frame));

% =========================================================================
% (2) SHELL DKT triangular - placa 4 m x 3 m, t = 0.15 m
% =========================================================================
fprintf('\n--- (2) Shell DKT triangular ---\n');

coordsShell = [0 0 0;
               4 0 0;
               0 3 0];

matShell = struct('E', E_c, 'nu', nu_c, 'G', G_c, 'thickness', 0.15);
matShell.useDST = false;        % Kirchhoff puro

elemShell = struct('type', 'shell_dkt', 'nodeIds', [1 2 3]);
K_shell = getLocalStiffnessMatrix(elemShell, coordsShell, matShell);
fprintf('  K_shell size: %dx%d  (esperado 18x18)\n', size(K_shell,1), size(K_shell,2));

% =========================================================================
% (3) SHELL Q4 isoparametrico  -  4 m x 2 m, t = 0.20 m
% =========================================================================
fprintf('\n--- (3) Shell Q4 isoparametrico ---\n');

coordsQ4 = [0 0 0;
            4 0 0;
            4 2 0;
            0 2 0];

matQ4 = struct('E', E_c, 'nu', nu_c, 'thickness', 0.20);

elemQ4 = struct('type', 'shell_q4', 'nodeIds', [1 2 3 4]);
K_q4 = getLocalStiffnessMatrix(elemQ4, coordsQ4, matQ4);
fprintf('  K_q4 size: %dx%d  (esperado 24x24)\n', size(K_q4,1), size(K_q4,2));

% =========================================================================
% (4) INTERFACE Goodman 1968 - junta losa-suelo
% =========================================================================
fprintf('\n--- (4) Interface Goodman 1968 ---\n');

coordsInt = [0 0 0;
             4 0 0;
             4 0 0;       % nodo bajo el 2 (zero-thickness)
             0 0 0];      % nodo bajo el 1

matInt = struct('isInterface', true, 'kn', 1e5, 'ks', 1e4);
elemInt = struct('type', 'interface', 'nodeIds', [1 2 3 4]);
K_int = getLocalStiffnessMatrix(elemInt, coordsInt, matInt);
fprintf('  K_int size: %dx%d  (esperado 12x12)\n', size(K_int,1), size(K_int,2));

% =========================================================================
% (5) Demo de modificadores
% =========================================================================
fprintf('\n--- (5) Modificadores: rotula en nodo 2 del frame ---\n');

% Liberamos rz2 (DOF 12 del frame, indice 12)
releases = false(12,1);
releases(12) = true;            % rotula en flexion plano xy del nodo 2

elemFrameHinge = elemFrame;
elemFrameHinge.releases = releases;
K_frame_hinge = getLocalStiffnessMatrix(elemFrameHinge, nodes, matFrame);
fprintf('  K_frame con rotula en rz2:  K(12,12) = %g  (debe ser ~0)\n', ...
        K_frame_hinge(12, 12));

% =========================================================================
fprintf('\n[OK] Demo completo.\n');
