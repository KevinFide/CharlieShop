-- ============================================================
-- CHARLIE SHOP
-- PROCEDIMIENTO ALMACENADO: DASHBOARD
-- PostgreSQL
-- ============================================================

DROP PROCEDURE IF EXISTS sp_obtener_dashboard(JSON);

CREATE OR REPLACE PROCEDURE sp_obtener_dashboard(
    OUT o_dashboard JSON
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_ventas_mes NUMERIC(12,2);
    v_total_productos INTEGER;
    v_total_clientes INTEGER;
    v_cuentas_por_cobrar NUMERIC(12,2);
    v_ventas_hoy NUMERIC(12,2);
    v_transacciones_hoy INTEGER;
    v_productos_stock_bajo INTEGER;
    v_cuentas_pendientes INTEGER;
    v_ventas_recientes JSON;
    v_ventas_ultimos_dias JSON;
    v_productos_bajo_stock JSON;
BEGIN
    SELECT COALESCE(SUM(total_neto), 0)
    INTO v_ventas_mes
    FROM venta
    WHERE estado <> 'CANCELADA'
      AND fecha >= date_trunc('month', CURRENT_DATE)
      AND fecha < date_trunc('month', CURRENT_DATE) + INTERVAL '1 month';

    SELECT COUNT(*)
    INTO v_total_productos
    FROM producto
    WHERE estado = TRUE;

    SELECT COUNT(*)
    INTO v_total_clientes
    FROM cliente
    WHERE estado = TRUE;

    SELECT COALESCE(SUM(saldo_pendiente), 0)
    INTO v_cuentas_por_cobrar
    FROM cuenta_por_cobrar
    WHERE saldo_pendiente > 0
      AND estado IN ('PENDIENTE', 'PARCIAL', 'VENCIDA');

    SELECT COALESCE(SUM(total_neto), 0)
    INTO v_ventas_hoy
    FROM venta
    WHERE estado <> 'CANCELADA'
      AND fecha >= CURRENT_DATE
      AND fecha < CURRENT_DATE + INTERVAL '1 day';

    SELECT COUNT(*)
    INTO v_transacciones_hoy
    FROM venta
    WHERE estado <> 'CANCELADA'
      AND fecha >= CURRENT_DATE
      AND fecha < CURRENT_DATE + INTERVAL '1 day';

    SELECT COUNT(*)
    INTO v_productos_stock_bajo
    FROM inventario i
    INNER JOIN producto p ON p.id_producto = i.producto_id
    WHERE p.estado = TRUE
      AND i.stock_actual <= i.stock_minimo;

    SELECT COUNT(*)
    INTO v_cuentas_pendientes
    FROM cuenta_por_cobrar
    WHERE saldo_pendiente > 0
      AND estado IN ('PENDIENTE', 'PARCIAL', 'VENCIDA');

    SELECT COALESCE(
        json_agg(
            json_build_object(
                'IdVenta', datos.id_venta,
                'Cliente', datos.cliente,
                'Fecha', datos.fecha,
                'TotalNeto', datos.total_neto,
                'Estado', datos.estado
            )
            ORDER BY datos.fecha DESC
        ),
        '[]'::JSON
    )
    INTO v_ventas_recientes
    FROM (
        SELECT
            v.id_venta,
            COALESCE(
                NULLIF(BTRIM(CONCAT_WS(' ', c.nombre, c.apellido)), ''),
                'Cliente general'
            ) AS cliente,
            v.fecha,
            v.total_neto,
            v.estado
        FROM venta v
        LEFT JOIN cliente c ON c.id_cliente = v.cliente_id
        ORDER BY v.fecha DESC
        LIMIT 6
    ) datos;

    SELECT COALESCE(
        json_agg(
            json_build_object(
                'Fecha', datos.fecha,
                'Total', datos.total
            )
            ORDER BY datos.fecha
        ),
        '[]'::JSON
    )
    INTO v_ventas_ultimos_dias
    FROM (
        SELECT
            meses.fecha::DATE AS fecha,
            COALESCE(SUM(v.total_neto), 0) AS total
        FROM generate_series(
            date_trunc('month', CURRENT_DATE) - INTERVAL '5 months',
            date_trunc('month', CURRENT_DATE),
            INTERVAL '1 month'
        ) AS meses(fecha)
        LEFT JOIN venta v
            ON v.fecha >= meses.fecha
           AND v.fecha < meses.fecha + INTERVAL '1 month'
           AND v.estado <> 'CANCELADA'
        GROUP BY meses.fecha
    ) datos;

    SELECT COALESCE(
        json_agg(
            json_build_object(
                'Nombre', datos.nombre,
                'StockActual', datos.stock_actual,
                'StockMinimo', datos.stock_minimo
            )
            ORDER BY datos.stock_actual, datos.nombre
        ),
        '[]'::JSON
    )
    INTO v_productos_bajo_stock
    FROM (
        SELECT
            p.nombre,
            i.stock_actual,
            i.stock_minimo
        FROM inventario i
        INNER JOIN producto p ON p.id_producto = i.producto_id
        WHERE p.estado = TRUE
          AND i.stock_actual <= i.stock_minimo
        ORDER BY i.stock_actual ASC, p.nombre
        LIMIT 5
    ) datos;

    o_dashboard := json_build_object(
        'VentasMes', v_ventas_mes,
        'TotalProductos', v_total_productos,
        'TotalClientes', v_total_clientes,
        'CuentasPorCobrar', v_cuentas_por_cobrar,
        'VentasHoy', v_ventas_hoy,
        'TransaccionesHoy', v_transacciones_hoy,
        'ProductosStockBajo', v_productos_stock_bajo,
        'CuentasPendientes', v_cuentas_pendientes,
        'VentasRecientes', v_ventas_recientes,
        'VentasUltimosMeses', v_ventas_ultimos_dias,
        'ProductosBajoStock', v_productos_bajo_stock
    );
END;
$$;

-- ============================================================
-- PRUEBA:
-- CALL sp_obtener_dashboard(NULL);
-- ============================================================
