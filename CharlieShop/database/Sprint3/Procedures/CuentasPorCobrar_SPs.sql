-- ============================================================
-- HU-36: REGISTRAR VENTA CON INTEGRACION DE CUENTAS POR COBRAR
-- ============================================================

DROP PROCEDURE IF EXISTS public.sp_registrar_venta_sprint3(
    INTEGER, VARCHAR, INTEGER, NUMERIC, JSON, DATE
);

CREATE PROCEDURE public.sp_registrar_venta_sprint3(
    IN p_cliente_id INTEGER,
    IN p_forma_pago VARCHAR,
    IN p_usuario_id INTEGER,
    IN p_descuento_porcentaje NUMERIC,
    IN p_productos JSON,
    IN p_fecha_vencimiento DATE,
    OUT o_resultado VARCHAR,
    OUT o_id_venta INTEGER
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_resultado VARCHAR;
    v_id_venta INTEGER;
    v_total_neto NUMERIC(12,2);
BEGIN
    o_resultado := NULL;
    o_id_venta := NULL;

    IF p_forma_pago IS NULL
       OR p_forma_pago NOT IN ('CONTADO', 'CREDITO') THEN
        RAISE EXCEPTION 'La forma de pago debe ser CONTADO o CREDITO.';
    END IF;

    IF p_forma_pago = 'CREDITO' THEN
        IF p_cliente_id IS NULL OR p_cliente_id <= 0 THEN
            RAISE EXCEPTION 'Una venta a credito requiere un cliente valido.';
        END IF;

        IF p_fecha_vencimiento IS NULL THEN
            RAISE EXCEPTION 'Debe indicar la fecha de vencimiento de la cuenta.';
        END IF;

        IF p_fecha_vencimiento < CURRENT_DATE THEN
            RAISE EXCEPTION 'La fecha de vencimiento no puede ser anterior a la fecha actual.';
        END IF;
    ELSIF p_fecha_vencimiento IS NOT NULL THEN
        RAISE EXCEPTION 'Las ventas de contado no deben tener fecha de vencimiento.';
    END IF;

    -- Reutiliza la logica de ventas, impuestos, detalles e inventario.
    CALL public.sp_registrar_venta(
        p_cliente_id,
        p_forma_pago,
        p_usuario_id,
        p_descuento_porcentaje,
        p_productos,
        v_resultado,
        v_id_venta
    );

    IF v_resultado IS DISTINCT FROM 'OK' OR v_id_venta IS NULL THEN
        RAISE EXCEPTION 'No fue posible registrar la venta correctamente.';
    END IF;

    IF p_forma_pago = 'CREDITO' THEN
        SELECT v.total_neto
        INTO v_total_neto
        FROM public.venta v
        WHERE v.id_venta = v_id_venta;

        IF NOT FOUND THEN
            RAISE EXCEPTION 'No fue posible recuperar el total de la venta.';
        END IF;

        INSERT INTO public.cuenta_por_cobrar (
            cliente_id, venta_id, fecha, fecha_vencimiento,
            total, saldo_pendiente, estado
        )
        VALUES (
            p_cliente_id, v_id_venta, CURRENT_DATE, p_fecha_vencimiento,
            v_total_neto, v_total_neto, 'PENDIENTE'
        );
    END IF;

    o_resultado := 'OK';
    o_id_venta := v_id_venta;
END;
$$;


-- ============================================================
-- HU-37: REGISTRAR PAGO Y ACTUALIZAR EL SALDO
-- ============================================================

DROP PROCEDURE IF EXISTS public.sp_registrar_pago_cuenta(
    INTEGER, NUMERIC, VARCHAR, VARCHAR, TEXT, INTEGER
);

CREATE PROCEDURE public.sp_registrar_pago_cuenta(
    IN p_id_cxc INTEGER,
    IN p_monto NUMERIC,
    IN p_metodo_pago VARCHAR,
    IN p_referencia VARCHAR,
    IN p_observaciones TEXT,
    IN p_usuario_id INTEGER,
    OUT o_resultado VARCHAR,
    OUT o_saldo_actualizado NUMERIC
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_saldo NUMERIC(12,2);
    v_nuevo_saldo NUMERIC(12,2);
BEGIN
    o_resultado := NULL;
    o_saldo_actualizado := NULL;

    IF p_monto IS NULL OR p_monto <= 0
       OR p_monto = 'NaN'::NUMERIC
       OR p_monto IN ('Infinity'::NUMERIC, '-Infinity'::NUMERIC) THEN
        o_resultado := 'MONTO_INVALIDO';
        RETURN;
    END IF;

    IF p_metodo_pago IS NULL OR BTRIM(p_metodo_pago) = '' THEN
        o_resultado := 'METODO_PAGO_INVALIDO';
        RETURN;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM public.usuario
        WHERE id_usuario = p_usuario_id AND estado = TRUE
    ) THEN
        o_resultado := 'USUARIO_INVALIDO';
        RETURN;
    END IF;

    SELECT saldo_pendiente
    INTO v_saldo
    FROM public.cuenta_por_cobrar
    WHERE id_cxc = p_id_cxc
    FOR UPDATE;

    IF NOT FOUND THEN
        o_resultado := 'CUENTA_NO_ENCONTRADA';
        RETURN;
    END IF;

    IF v_saldo <= 0 THEN
        o_resultado := 'CUENTA_PAGADA';
        o_saldo_actualizado := 0;
        RETURN;
    END IF;

    IF ROUND(p_monto, 2) > v_saldo THEN
        o_resultado := 'MONTO_MAYOR_AL_SALDO';
        o_saldo_actualizado := v_saldo;
        RETURN;
    END IF;

    INSERT INTO public.pago (
        cxc_id, fecha_pago, monto, metodo_pago,
        referencia, observaciones, usuario_id
    )
    VALUES (
        p_id_cxc, CURRENT_DATE, ROUND(p_monto, 2),
        BTRIM(p_metodo_pago), NULLIF(BTRIM(p_referencia), ''),
        NULLIF(BTRIM(p_observaciones), ''), p_usuario_id
    );

    v_nuevo_saldo := ROUND(v_saldo - p_monto, 2);

    UPDATE public.cuenta_por_cobrar
    SET saldo_pendiente = v_nuevo_saldo,
        estado = CASE
            WHEN v_nuevo_saldo = 0 THEN 'PAGADA'
            WHEN CURRENT_DATE > fecha_vencimiento THEN 'VENCIDA'
            ELSE 'PARCIAL'
        END
    WHERE id_cxc = p_id_cxc;

    o_resultado := 'OK';
    o_saldo_actualizado := v_nuevo_saldo;
END;
$$;


-- ============================================================
-- HU-38: LISTAR CUENTAS PENDIENTES CON FILTROS
-- ============================================================

DROP PROCEDURE IF EXISTS public.sp_listar_cuentas_por_cobrar(
    VARCHAR, VARCHAR
);

CREATE PROCEDURE public.sp_listar_cuentas_por_cobrar(
    IN p_busqueda VARCHAR,
    IN p_estado VARCHAR,
    INOUT o_cuentas TEXT
)
LANGUAGE plpgsql
AS $$
BEGIN
    SELECT COALESCE(
        JSON_AGG(
            JSON_BUILD_OBJECT(
                'IdCxc', cxc.id_cxc,
                'IdCliente', c.id_cliente,
                'NombreCliente', CONCAT_WS(' ', c.nombre, c.apellido),
                'Identificacion', c.identificacion,
                'IdVenta', v.id_venta,
                'Fecha', cxc.fecha,
                'FechaVencimiento', cxc.fecha_vencimiento,
                'Total', cxc.total,
                'SaldoPendiente', cxc.saldo_pendiente,
                'Estado', CASE
                    WHEN CURRENT_DATE > cxc.fecha_vencimiento THEN 'VENCIDA'
                    ELSE cxc.estado
                END,
                'DiasVencida', CASE
                    WHEN CURRENT_DATE > cxc.fecha_vencimiento
                    THEN CURRENT_DATE - cxc.fecha_vencimiento
                    ELSE 0
                END
            )
            ORDER BY cxc.fecha DESC, cxc.id_cxc DESC
        ),
        '[]'
    )::TEXT
    INTO o_cuentas
    FROM public.cuenta_por_cobrar cxc
    INNER JOIN public.cliente c ON c.id_cliente = cxc.cliente_id
    INNER JOIN public.venta v ON v.id_venta = cxc.venta_id
    WHERE cxc.saldo_pendiente > 0
      AND (
          NULLIF(BTRIM(p_busqueda), '') IS NULL
          OR c.nombre ILIKE '%' || BTRIM(p_busqueda) || '%'
          OR c.apellido ILIKE '%' || BTRIM(p_busqueda) || '%'
          OR CONCAT_WS(' ', c.nombre, c.apellido) ILIKE '%' || BTRIM(p_busqueda) || '%'
          OR c.identificacion ILIKE '%' || BTRIM(p_busqueda) || '%'
          OR CAST(v.id_venta AS VARCHAR) ILIKE '%' || BTRIM(p_busqueda) || '%'
      )
      AND (
          UPPER(COALESCE(NULLIF(BTRIM(p_estado), ''), 'TODOS')) = 'TODOS'
          OR (
              UPPER(p_estado) = 'VENCIDA'
              AND CURRENT_DATE > cxc.fecha_vencimiento
          )
          OR (
              UPPER(p_estado) = 'PENDIENTE'
              AND cxc.estado = 'PENDIENTE'
              AND CURRENT_DATE <= cxc.fecha_vencimiento
          )
          OR (
              UPPER(p_estado) = 'PARCIAL'
              AND cxc.estado = 'PARCIAL'
              AND CURRENT_DATE <= cxc.fecha_vencimiento
          )
      );
END;
$$;


-- ============================================================
-- HU-39: BUSCAR CUENTAS POR CLIENTE
-- ============================================================

DROP PROCEDURE IF EXISTS public.sp_buscar_cuentas_por_cobrar(
    VARCHAR, TEXT
);

CREATE PROCEDURE public.sp_buscar_cuentas_por_cobrar(
    IN p_busqueda VARCHAR,
    INOUT o_cuentas TEXT
)
LANGUAGE plpgsql
AS $$
BEGIN
    SELECT COALESCE(
        JSON_AGG(
            JSON_BUILD_OBJECT(
                'IdCxc', cxc.id_cxc,
                'IdCliente', c.id_cliente,
                'NombreCliente', CONCAT_WS(' ', c.nombre, c.apellido),
                'Identificacion', c.identificacion,
                'Telefono', c.telefono,
                'IdVenta', v.id_venta,
                'Fecha', cxc.fecha,
                'FechaVencimiento', cxc.fecha_vencimiento,
                'Total', cxc.total,
                'SaldoPendiente', cxc.saldo_pendiente,
                'Estado', CASE
                    WHEN CURRENT_DATE > cxc.fecha_vencimiento THEN 'VENCIDA'
                    ELSE cxc.estado
                END
            )
            ORDER BY cxc.fecha DESC
        ),
        '[]'
    )::TEXT
    INTO o_cuentas
    FROM public.cuenta_por_cobrar cxc
    INNER JOIN public.cliente c ON c.id_cliente = cxc.cliente_id
    INNER JOIN public.venta v ON v.id_venta = cxc.venta_id
    WHERE cxc.saldo_pendiente > 0
      AND (
          NULLIF(BTRIM(p_busqueda), '') IS NULL
          OR c.nombre ILIKE '%' || BTRIM(p_busqueda) || '%'
          OR c.apellido ILIKE '%' || BTRIM(p_busqueda) || '%'
          OR CONCAT_WS(' ', c.nombre, c.apellido) ILIKE '%' || BTRIM(p_busqueda) || '%'
          OR c.identificacion ILIKE '%' || BTRIM(p_busqueda) || '%'
          OR c.telefono ILIKE '%' || BTRIM(p_busqueda) || '%'
      );
END;
$$;


-- ============================================================
-- HU-40: DETALLE DE CUENTA Y SUS PAGOS
-- ============================================================

DROP PROCEDURE IF EXISTS public.sp_obtener_detalle_cuenta(
    INTEGER, TEXT
);

CREATE PROCEDURE public.sp_obtener_detalle_cuenta(
    IN p_id_cxc INTEGER,
    INOUT o_detalle TEXT
)
LANGUAGE plpgsql
AS $$
BEGIN
    SELECT CASE
        WHEN cxc.id_cxc IS NULL THEN NULL
        ELSE JSON_BUILD_OBJECT(
            'IdCxc', cxc.id_cxc,
            'IdCliente', c.id_cliente,
            'NombreCliente', CONCAT_WS(' ', c.nombre, c.apellido),
            'Identificacion', c.identificacion,
            'Telefono', c.telefono,
            'Email', c.email,
            'IdVenta', v.id_venta,
            'Fecha', cxc.fecha,
            'FechaVencimiento', cxc.fecha_vencimiento,
            'Total', cxc.total,
            'SaldoPendiente', cxc.saldo_pendiente,
            'Estado', CASE
                WHEN cxc.saldo_pendiente > 0
                     AND CURRENT_DATE > cxc.fecha_vencimiento THEN 'VENCIDA'
                ELSE cxc.estado
            END,
            'FormaPago', v.forma_pago,
            'Pagos', COALESCE((
                SELECT JSON_AGG(
                    JSON_BUILD_OBJECT(
                        'IdPago', pg.id_pago,
                        'FechaPago', pg.fecha_pago,
                        'Monto', pg.monto,
                        'MetodoPago', pg.metodo_pago,
                        'Referencia', pg.referencia,
                        'Observaciones', pg.observaciones,
                        'Usuario', u.nombre
                    )
                    ORDER BY pg.fecha_pago DESC, pg.id_pago DESC
                )
                FROM public.pago pg
                INNER JOIN public.usuario u ON u.id_usuario = pg.usuario_id
                WHERE pg.cxc_id = cxc.id_cxc
            ), '[]'::JSON)
        )::TEXT
    END
    INTO o_detalle
    FROM public.cuenta_por_cobrar cxc
    INNER JOIN public.cliente c ON c.id_cliente = cxc.cliente_id
    INNER JOIN public.venta v ON v.id_venta = cxc.venta_id
    WHERE cxc.id_cxc = p_id_cxc;
END;
$$;


-- ============================================================
-- HU-41: LISTAR CUENTAS VENCIDAS
-- ============================================================

DROP PROCEDURE IF EXISTS public.sp_listar_cuentas_vencidas(
    VARCHAR, TEXT
);

CREATE PROCEDURE public.sp_listar_cuentas_vencidas(
    IN p_busqueda VARCHAR,
    INOUT o_cuentas TEXT
)
LANGUAGE plpgsql
AS $$
BEGIN
    SELECT COALESCE(
        JSON_AGG(
            JSON_BUILD_OBJECT(
                'IdCxc', cxc.id_cxc,
                'IdCliente', c.id_cliente,
                'NombreCliente', CONCAT_WS(' ', c.nombre, c.apellido),
                'Identificacion', c.identificacion,
                'Telefono', c.telefono,
                'Email', c.email,
                'IdVenta', v.id_venta,
                'Fecha', cxc.fecha,
                'FechaVencimiento', cxc.fecha_vencimiento,
                'Total', cxc.total,
                'SaldoPendiente', cxc.saldo_pendiente,
                'Estado', 'VENCIDA',
                'DiasVencida', CURRENT_DATE - cxc.fecha_vencimiento
            )
            ORDER BY cxc.fecha_vencimiento ASC
        ),
        '[]'
    )::TEXT
    INTO o_cuentas
    FROM public.cuenta_por_cobrar cxc
    INNER JOIN public.cliente c ON c.id_cliente = cxc.cliente_id
    INNER JOIN public.venta v ON v.id_venta = cxc.venta_id
    WHERE cxc.saldo_pendiente > 0
      AND CURRENT_DATE > cxc.fecha_vencimiento
      AND (
          NULLIF(BTRIM(p_busqueda), '') IS NULL
          OR c.nombre ILIKE '%' || BTRIM(p_busqueda) || '%'
          OR c.apellido ILIKE '%' || BTRIM(p_busqueda) || '%'
          OR CONCAT_WS(' ', c.nombre, c.apellido) ILIKE '%' || BTRIM(p_busqueda) || '%'
          OR c.identificacion ILIKE '%' || BTRIM(p_busqueda) || '%'
      );
END;
$$;


-- ============================================================
-- HU-42: HISTORIAL DE PAGOS
-- Filtros opcionales: cliente y cuenta por cobrar.
-- ============================================================

DROP PROCEDURE IF EXISTS public.sp_consultar_historial_pagos(
    INTEGER, INTEGER, TEXT
);

CREATE PROCEDURE public.sp_consultar_historial_pagos(
    IN p_id_cliente INTEGER,
    IN p_id_cxc INTEGER,
    INOUT o_pagos TEXT
)
LANGUAGE plpgsql
AS $$
BEGIN
    SELECT COALESCE(
        JSON_AGG(
            JSON_BUILD_OBJECT(
                'IdPago', pg.id_pago,
                'IdCxc', cxc.id_cxc,
                'IdCliente', c.id_cliente,
                'NombreCliente', CONCAT_WS(' ', c.nombre, c.apellido),
                'Identificacion', c.identificacion,
                'IdVenta', v.id_venta,
                'FechaPago', pg.fecha_pago,
                'Monto', pg.monto,
                'MetodoPago', pg.metodo_pago,
                'Referencia', pg.referencia,
                'Observaciones', pg.observaciones,
                'Usuario', u.nombre
            )
            ORDER BY pg.fecha_pago DESC, pg.id_pago DESC
        ),
        '[]'
    )::TEXT
    INTO o_pagos
    FROM public.pago pg
    INNER JOIN public.cuenta_por_cobrar cxc ON cxc.id_cxc = pg.cxc_id
    INNER JOIN public.cliente c ON c.id_cliente = cxc.cliente_id
    INNER JOIN public.venta v ON v.id_venta = cxc.venta_id
    INNER JOIN public.usuario u ON u.id_usuario = pg.usuario_id
    WHERE (p_id_cliente IS NULL OR cxc.cliente_id = p_id_cliente)
      AND (p_id_cxc IS NULL OR cxc.id_cxc = p_id_cxc);
END;
$$;


-- ============================================================
-- FIN DEL ARCHIVO DE PROCEDIMIENTOS - SPRINT 3
-- ============================================================
