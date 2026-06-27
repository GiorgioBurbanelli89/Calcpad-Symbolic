function Bs = getShearStrainDisplacementMatrix(CS)
% getShearStrainDisplacementMatrix
% -------------------------------------------------------------------------
% Matriz B_s 2x9 que relaciona DOF de placa con deformacion por CORTE
% transversal (gamma_xz, gamma_yz). Solo se usa en el DST (Mindlin) — para
% DKT puro (Kirchhoff) Bs es identicamente cero.
%
% Para un triangulo lineal con campo de giros constante por elemento, las
% deformaciones de corte son tambien constantes:
%
%   gamma_xz = dw/dx - theta_y
%   gamma_yz = dw/dy + theta_x
%
% Como w es lineal -> dw/dx, dw/dy son constantes. theta_x, theta_y se
% interpolan linealmente con N_i = L_i (las coords baricentricas).
%
% Salida: Bs 2x9, indices {w1 thx1 thy1 w2 thx2 thy2 w3 thx3 thy3}
% -------------------------------------------------------------------------

    A  = CS.area;
    b1=CS.b1; b2=CS.b2; b3=CS.b3;
    c1=CS.c1; c2=CS.c2; c3=CS.c3;
    inv2A = 1 / (2 * A);

    % derivadas de N_i = L_i: dL_i/dx = b_i/(2A), dL_i/dy = c_i/(2A)
    % En centro: theta_x = (thx1+thx2+thx3)/3, idem theta_y.

    % gamma_xz = sum_i (b_i/(2A))*w_i - sum_i N_i*theta_y_i
    % gamma_yz = sum_i (c_i/(2A))*w_i + sum_i N_i*theta_x_i
    % Evaluamos en el centroide donde N_i = 1/3.

    third = 1/3;
    Bs = [ b1*inv2A   0        -third      b2*inv2A   0        -third      b3*inv2A   0        -third;
           c1*inv2A   third     0          c2*inv2A   third     0          c3*inv2A   third     0      ];
end
