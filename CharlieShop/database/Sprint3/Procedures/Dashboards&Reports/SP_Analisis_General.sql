-- ============================================================
-- CHARLIE SHOP - ANALISIS GENERAL DE REPORTES
-- Panel de KPI, gráficos y tablas analíticas.
-- Todas las consultas se ejecutan desde este procedimiento.
-- ============================================================

DROP PROCEDURE IF EXISTS public.sp_obtener_analisis_general(DATE, DATE);

CREATE OR REPLACE PROCEDURE public.sp_obtener_analisis_general(
    IN p_fecha_inicio DATE,
    IN p_fecha_fin DATE,
    OUT o_resultado JSON
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_ticket_promedio NUMERIC(12,2);
    v_unidades_vendidas NUMERIC(12,2);
    v_clientes_compradores INTEGER;
    v_ventas_credito NUMERIC(12,2);
    v_ventas_contado NUMERIC(12,2);
    v_cantidad_ventas INTEGER;
    v_ventas_por_dia JSON;
    v_formas_pago JSON;
    v_productos_top JSON;
    v_movimientos_tipo JSON;
    v_clientes_top JSON;
    v_ultimos_movimientos JSON;
BEGIN
    IF p_fecha_inicio IS NULL OR p_fecha_fin IS NULL
       OR p_fecha_inicio > p_fecha_fin THEN
        RAISE EXCEPTION 'El período seleccionado no es válido.';
    END IF;

    -- KPI: ticket promedio del período.
    SELECT
        COALESCE(AVG(v.total_neto), 0),
        COUNT(*)::INTEGER
    INTO v_ticket_promedio, v_cantidad_ventas
    FROM venta v
    WHERE v.fecha >= p_fecha_inicio
      AND v.fecha < p_fecha_fin + INTERVAL '1 day'
      AND v.estado <> 'CANCELADA';

    -- KPI: unidades vendidas en el período.
    SELECT COALESCE(SUM(dv.cantidad), 0)
    INTO v_unidades_vendidas
    FROM detalle_venta dv
    INNER JOIN venta v ON v.id_venta = dv.venta_id
    WHERE v.fecha >= p_fecha_inicio
      AND v.fecha < p_fecha_fin + INTERVAL '1 day'
      AND v.estado <> 'CANCELADA';

    -- KPI: clientes únicos con compras en el período.
    SELECT COUNT(DISTINCT v.cliente_id)::INTEGER
    INTO v_clientes_compradores
    FROM venta v
    WHERE v.fecha >= p_fecha_inicio
      AND v.fecha < p_fecha_fin + INTERVAL '1 day'
      AND v.estado <> 'CANCELADA'
      AND v.cliente_id IS NOT NULL;

    -- KPI: monto vendido a crédito y contado.
    SELECT
        COALESCE(SUM(v.total_neto) FILTER (WHERE v.forma_pago = 'CREDITO'), 0),
        COALESCE(SUM(v.total_neto) FILTER (WHERE v.forma_pago = 'CONTADO'), 0)
    INTO v_ventas_credito, v_ventas_contado
    FROM venta v
    WHERE v.fecha >= p_fecha_inicio
      AND v.fecha < p_fecha_fin + INTERVAL '1 day'
      AND v.estado <> 'CANCELADA';

    -- Gráfico: evolución diaria de ventas. Los días sin ventas se muestran en cero.
    SELECT COALESCE(
        json_agg(
            json_build_object(
                'Fecha', serie.fecha,
                'Total', serie.total
            )
            ORDER BY serie.fecha
        ),
        '[]'::JSON
    )
    INTO v_ventas_por_dia
    FROM (
        SELECT
            calendario.dia::DATE AS fecha,
            COALESCE(SUM(v.total_neto), 0) AS total
        FROM generate_series(
            p_fecha_inicio::TIMESTAMP,
            p_fecha_fin::TIMESTAMP,
            INTERVAL '1 day'
        ) AS calendario(dia)
        LEFT JOIN venta v
            ON v.fecha >= calendario.dia
           AND v.fecha < calendario.dia + INTERVAL '1 day'
           AND v.estado <> 'CANCELADA'
        GROUP BY calendario.dia
        ORDER BY calendario.dia
    ) serie;

    -- Gráfico: distribución por forma de pago.
    SELECT COALESCE(
        json_agg(
            json_build_object(
                'FormaPago', pagos.forma_pago,
                'Cantidad', pagos.cantidad,
                'Total', pagos.total
            )
            ORDER BY pagos.forma_pago
        ),
        '[]'::JSON
    )
    INTO v_formas_pago
    FROM (
        SELECT
            v.forma_pago,
            COUNT(*)::INTEGER AS cantidad,
            COALESCE(SUM(v.total_neto), 0) AS total
        FROM venta v
        WHERE v.fecha >= p_fecha_inicio
          AND v.fecha < p_fecha_fin + INTERVAL '1 day'
          AND v.estado <> 'CANCELADA'
        GROUP BY v.forma_pago
    ) pagos;

    -- Gráfico/tabla: cinco productos con mayor facturación del período.
    SELECT COALESCE(
        json_agg(
            json_build_object(
                'Producto', top.producto,
                'Categoria', top.categoria,
                'Unidades', top.unidades,
                'TotalVendido', top.total_vendido
            )
            ORDER BY top.total_vendido DESC, top.unidades DESC
        ),
        '[]'::JSON
    )
    INTO v_productos_top
    FROM (
        SELECT
            p.nombre AS producto,
            COALESCE(p.categoria, 'Sin categoría') AS categoria,
            SUM(dv.cantidad) AS unidades,
            SUM(dv.subtotal) AS total_vendido
        FROM detalle_venta dv
        INNER JOIN venta v ON v.id_venta = dv.venta_id
        INNER JOIN producto p ON p.id_producto = dv.producto_id
        WHERE v.fecha >= p_fecha_inicio
          AND v.fecha < p_fecha_fin + INTERVAL '1 day'
          AND v.estado <> 'CANCELADA'
        GROUP BY p.id_producto, p.nombre, p.categoria
        ORDER BY total_vendido DESC, unidades DESC
        LIMIT 5
    ) top;

    -- Gráfico: movimientos de inventario por tipo y cantidad acumulada.
    SELECT COALESCE(
        json_agg(
            json_build_object(
                'TipoMovimiento', resumen.tipo_movimiento,
                'CantidadMovimientos', resumen.cantidad_movimientos,
                'Unidades', resumen.unidades
            )
            ORDER BY resumen.tipo_movimiento
        ),
        '[]'::JSON
    )
    INTO v_movimientos_tipo
    FROM (
        SELECT
            mi.tipo_movimiento,
            COUNT(*)::INTEGER AS cantidad_movimientos,
            COALESCE(SUM(mi.cantidad), 0) AS unidades
        FROM movimiento_inventario mi
        WHERE mi.fecha >= p_fecha_inicio
          AND mi.fecha < p_fecha_fin + INTERVAL '1 day'
        GROUP BY mi.tipo_movimiento
    ) resumen;

    -- Tabla: cinco clientes con mayor monto de compras.
    SELECT COALESCE(
        json_agg(
            json_build_object(
                'Cliente', clientes.cliente,
                'CantidadCompras', clientes.cantidad_compras,
                'TotalCompras', clientes.total_compras
            )
            ORDER BY clientes.total_compras DESC
        ),
        '[]'::JSON
    )
    INTO v_clientes_top
    FROM (
        SELECT
            BTRIM(CONCAT_WS(' ', c.nombre, c.apellido)) AS cliente,
            COUNT(v.id_venta)::INTEGER AS cantidad_compras,
            COALESCE(SUM(v.total_neto), 0) AS total_compras
        FROM cliente c
        INNER JOIN venta v ON v.cliente_id = c.id_cliente
        WHERE v.fecha >= p_fecha_inicio
          AND v.fecha < p_fecha_fin + INTERVAL '1 day'
          AND v.estado <> 'CANCELADA'
        GROUP BY c.id_cliente, c.nombre, c.apellido
        ORDER BY total_compras DESC
        LIMIT 5
    ) clientes;

    -- Tabla: últimos movimientos de inventario del período.
    SELECT COALESCE(
        json_agg(
            json_build_object(
                'Fecha', movimientos.fecha,
                'Producto', movimientos.producto,
                'TipoMovimiento', movimientos.tipo_movimiento,
                'Cantidad', movimientos.cantidad,
                'Referencia', movimientos.referencia
            )
            ORDER BY movimientos.fecha DESC
        ),
        '[]'::JSON
    )
    INTO v_ultimos_movimientos
    FROM (
        SELECT
            mi.fecha,
            p.nombre AS producto,
            mi.tipo_movimiento,
            mi.cantidad,
            mi.referencia
        FROM movimiento_inventario mi
        INNER JOIN inventario i ON i.id_inventario = mi.inventario_id
        INNER JOIN producto p ON p.id_producto = i.producto_id
        WHERE mi.fecha >= p_fecha_inicio
          AND mi.fecha < p_fecha_fin + INTERVAL '1 day'
        ORDER BY mi.fecha DESC
        LIMIT 6
    ) movimientos;

    o_resultado := json_build_object(
        'TicketPromedio', v_ticket_promedio,
        'UnidadesVendidas', v_unidades_vendidas,
        'ClientesCompradores', v_clientes_compradores,
        'VentasCredito', v_ventas_credito,
        'VentasContado', v_ventas_contado,
        'CantidadVentas', v_cantidad_ventas,
        'VentasPorDia', v_ventas_por_dia,
        'FormasPago', v_formas_pago,
        'ProductosTop', v_productos_top,
        'MovimientosPorTipo', v_movimientos_tipo,
        'ClientesTop', v_clientes_top,
        'UltimosMovimientos', v_ultimos_movimientos
    );
END;
$$;

-- PRUEBA:
-- CALL public.sp_obtener_analisis_general(
--     CURRENT_DATE - INTERVAL '29 days',
--     CURRENT_DATE,
--     NULL
-- );
