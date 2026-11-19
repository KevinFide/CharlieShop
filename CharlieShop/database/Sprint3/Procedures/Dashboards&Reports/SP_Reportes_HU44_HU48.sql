-- ============================================================
-- CHARLIE SHOP - PROCEDIMIENTOS ALMACENADOS DE REPORTES
-- HU-44 a HU-48
-- PostgreSQL
-- ============================================================

DROP PROCEDURE IF EXISTS sp_reporte_ventas(DATE, DATE);
DROP PROCEDURE IF EXISTS sp_reporte_productos_mas_vendidos();
DROP PROCEDURE IF EXISTS sp_reporte_inventario();
DROP PROCEDURE IF EXISTS sp_reporte_cuentas_por_cobrar();
DROP PROCEDURE IF EXISTS sp_reporte_clientes();

-- HU-44: Reporte de ventas por período.
CREATE OR REPLACE PROCEDURE sp_reporte_ventas(
    IN p_fecha_inicio DATE,
    IN p_fecha_fin DATE,
    OUT o_resultado JSON
)
LANGUAGE plpgsql
AS $$
BEGIN
    IF p_fecha_inicio IS NULL OR p_fecha_fin IS NULL
       OR p_fecha_inicio > p_fecha_fin THEN
        RAISE EXCEPTION 'El período de fechas no es válido.';
    END IF;

    SELECT COALESCE(
        json_agg(
            json_build_object(
                'IdVenta', datos.id_venta,
                'Fecha', datos.fecha,
                'Cliente', datos.cliente,
                'TotalNeto', datos.total_neto,
                'FormaPago', datos.forma_pago,
                'Estado', datos.estado
            )
            ORDER BY datos.fecha DESC
        ),
        '[]'::JSON
    )
    INTO o_resultado
    FROM (
        SELECT
            v.id_venta,
            v.fecha,
            COALESCE(
                NULLIF(BTRIM(CONCAT_WS(' ', c.nombre, c.apellido)), ''),
                'Cliente general'
            ) AS cliente,
            v.total_neto,
            v.forma_pago,
            v.estado
        FROM venta v
        LEFT JOIN cliente c ON c.id_cliente = v.cliente_id
        WHERE v.fecha >= p_fecha_inicio
          AND v.fecha < p_fecha_fin + INTERVAL '1 day'
          AND v.estado <> 'CANCELADA'
        ORDER BY v.fecha DESC
    ) datos;
END;
$$;

-- HU-45: Productos más vendidos, ordenados por unidades.
CREATE OR REPLACE PROCEDURE sp_reporte_productos_mas_vendidos(
    OUT o_resultado JSON
)
LANGUAGE plpgsql
AS $$
BEGIN
    SELECT COALESCE(
        json_agg(
            json_build_object(
                'Producto', datos.producto,
                'Categoria', datos.categoria,
                'CantidadVendida', datos.cantidad_vendida,
                'TotalVendido', datos.total_vendido
            )
            ORDER BY datos.cantidad_vendida DESC, datos.total_vendido DESC
        ),
        '[]'::JSON
    )
    INTO o_resultado
    FROM (
        SELECT
            p.nombre AS producto,
            COALESCE(p.categoria, 'Sin categoría') AS categoria,
            COALESCE(SUM(dv.cantidad), 0) AS cantidad_vendida,
            COALESCE(SUM(dv.subtotal), 0) AS total_vendido
        FROM detalle_venta dv
        INNER JOIN venta v ON v.id_venta = dv.venta_id
        INNER JOIN producto p ON p.id_producto = dv.producto_id
        WHERE v.estado <> 'CANCELADA'
        GROUP BY p.id_producto, p.nombre, p.categoria
        ORDER BY cantidad_vendida DESC, total_vendido DESC
    ) datos;
END;
$$;

-- HU-46: Existencias actuales del inventario.
CREATE OR REPLACE PROCEDURE sp_reporte_inventario(
    OUT o_resultado JSON
)
LANGUAGE plpgsql
AS $$
BEGIN
    SELECT COALESCE(
        json_agg(
            json_build_object(
                'Codigo', datos.codigo,
                'Producto', datos.producto,
                'Categoria', datos.categoria,
                'StockActual', datos.stock_actual,
                'StockMinimo', datos.stock_minimo,
                'StockMaximo', datos.stock_maximo,
                'EstadoStock', datos.estado_stock
            )
            ORDER BY datos.producto
        ),
        '[]'::JSON
    )
    INTO o_resultado
    FROM (
        SELECT
            p.codigo,
            p.nombre AS producto,
            COALESCE(p.categoria, 'Sin categoría') AS categoria,
            i.stock_actual,
            i.stock_minimo,
            i.stock_maximo,
            CASE
                WHEN i.stock_actual <= 0 THEN 'AGOTADO'
                WHEN i.stock_actual <= i.stock_minimo THEN 'BAJO'
                ELSE 'NORMAL'
            END AS estado_stock
        FROM inventario i
        INNER JOIN producto p ON p.id_producto = i.producto_id
        WHERE p.estado = TRUE
        ORDER BY p.nombre
    ) datos;
END;
$$;

-- HU-47: Cuentas por cobrar con saldo pendiente.
CREATE OR REPLACE PROCEDURE sp_reporte_cuentas_por_cobrar(
    OUT o_resultado JSON
)
LANGUAGE plpgsql
AS $$
BEGIN
    SELECT COALESCE(
        json_agg(
            json_build_object(
                'IdCuenta', datos.id_cxc,
                'IdVenta', datos.venta_id,
                'Cliente', datos.cliente,
                'Fecha', datos.fecha,
                'FechaVencimiento', datos.fecha_vencimiento,
                'Total', datos.total,
                'SaldoPendiente', datos.saldo_pendiente,
                'Estado', datos.estado,
                'DiasVencida', datos.dias_vencida
            )
            ORDER BY datos.fecha_vencimiento ASC
        ),
        '[]'::JSON
    )
    INTO o_resultado
    FROM (
        SELECT
            cx.id_cxc,
            cx.venta_id,
            BTRIM(CONCAT_WS(' ', c.nombre, c.apellido)) AS cliente,
            cx.fecha,
            cx.fecha_vencimiento,
            cx.total,
            cx.saldo_pendiente,
            cx.estado,
            CASE
                WHEN cx.fecha_vencimiento < CURRENT_DATE
                     AND cx.saldo_pendiente > 0
                THEN CURRENT_DATE - cx.fecha_vencimiento
                ELSE 0
            END AS dias_vencida
        FROM cuenta_por_cobrar cx
        INNER JOIN cliente c ON c.id_cliente = cx.cliente_id
        WHERE cx.saldo_pendiente > 0
        ORDER BY cx.fecha_vencimiento ASC
    ) datos;
END;
$$;

-- HU-48: Clientes registrados y resumen de compras.
CREATE OR REPLACE PROCEDURE sp_reporte_clientes(
    OUT o_resultado JSON
)
LANGUAGE plpgsql
AS $$
BEGIN
    SELECT COALESCE(
        json_agg(
            json_build_object(
                'IdCliente', datos.id_cliente,
                'NombreCompleto', datos.nombre_completo,
                'Identificacion', datos.identificacion,
                'Telefono', datos.telefono,
                'Email', datos.email,
                'FechaRegistro', datos.fecha_registro,
                'CantidadCompras', datos.cantidad_compras,
                'TotalCompras', datos.total_compras,
                'SaldoPendiente', datos.saldo_pendiente
            )
            ORDER BY datos.nombre_completo
        ),
        '[]'::JSON
    )
    INTO o_resultado
    FROM (
        SELECT
            c.id_cliente,
            BTRIM(CONCAT_WS(' ', c.nombre, c.apellido)) AS nombre_completo,
            c.identificacion,
            c.telefono,
            c.email,
            c.fecha_registro,
            COUNT(DISTINCT v.id_venta) FILTER (
                WHERE v.estado <> 'CANCELADA'
            )::INTEGER AS cantidad_compras,
            COALESCE(SUM(v.total_neto) FILTER (
                WHERE v.estado <> 'CANCELADA'
            ), 0) AS total_compras,
            COALESCE((
                SELECT SUM(cx.saldo_pendiente)
                FROM cuenta_por_cobrar cx
                WHERE cx.cliente_id = c.id_cliente
                  AND cx.saldo_pendiente > 0
            ), 0) AS saldo_pendiente
        FROM cliente c
        LEFT JOIN venta v ON v.cliente_id = c.id_cliente
        WHERE c.estado = TRUE
        GROUP BY
            c.id_cliente, c.nombre, c.apellido,
            c.identificacion, c.telefono, c.email, c.fecha_registro
        ORDER BY nombre_completo
    ) datos;
END;
$$;

-- ============================================================
-- PRUEBAS:
-- CALL sp_reporte_ventas(CURRENT_DATE - 30, CURRENT_DATE, NULL);
-- CALL sp_reporte_productos_mas_vendidos(NULL);
-- CALL sp_reporte_inventario(NULL);
-- CALL sp_reporte_cuentas_por_cobrar(NULL);
-- CALL sp_reporte_clientes(NULL);
-- ============================================================
