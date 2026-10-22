-- Procedimientos almacenados de inventario para Charlie Shop.

CREATE OR REPLACE PROCEDURE public.sp_ajustar_inventario(
	IN p_id_producto integer,
	IN p_direccion character varying,
	IN p_cantidad numeric,
	IN p_referencia character varying,
	IN p_observaciones text,
	IN p_usuario_id integer,
	OUT o_resultado character varying)
LANGUAGE 'plpgsql'
AS $BODY$

DECLARE
    v_id_inventario INTEGER;
    v_stock_actual NUMERIC(12,2);
    v_tipo_movimiento VARCHAR(20);
    v_observaciones TEXT;
BEGIN
    -- Validar la dirección del movimiento
    IF p_direccion NOT IN ('ENTRADA', 'SALIDA') THEN
        o_resultado := 'Error: La dirección debe ser ENTRADA o SALIDA.';
        RETURN;
    END IF;

    -- Validar la cantidad
    IF p_cantidad IS NULL OR p_cantidad <= 0 THEN
        o_resultado := 'Error: La cantidad debe ser mayor que cero.';
        RETURN;
    END IF;

    -- Determinar el tipo real del movimiento
    v_tipo_movimiento := p_direccion;

    -- Buscar el inventario del producto
    SELECT i.id_inventario, i.stock_actual
    INTO v_id_inventario, v_stock_actual
    FROM inventario i
    WHERE i.producto_id = p_id_producto
    FOR UPDATE;

    -- Si no existe inventario, permitir crearlo solamente para entradas
    IF NOT FOUND THEN

        IF p_direccion = 'SALIDA' THEN
            o_resultado := 'Error: El producto no tiene inventario registrado.';
            RETURN;
        END IF;

        INSERT INTO inventario (
            producto_id,
            stock_actual,
            stock_minimo,
            fecha_actualizacion
        )
        VALUES (
            p_id_producto,
            0,
            0,
            CURRENT_TIMESTAMP
        )
        RETURNING id_inventario, stock_actual
        INTO v_id_inventario, v_stock_actual;

    END IF;

    -- Validar que exista suficiente stock para una salida
    IF p_direccion = 'SALIDA'
       AND v_stock_actual < p_cantidad THEN

        o_resultado := 'Error: No hay suficiente inventario para realizar la salida.';
        RETURN;
    END IF;

    -- Actualizar las existencias
    IF p_direccion = 'ENTRADA' THEN

        UPDATE inventario
        SET stock_actual = stock_actual + p_cantidad,
            fecha_actualizacion = CURRENT_TIMESTAMP
        WHERE id_inventario = v_id_inventario;

    ELSE

        UPDATE inventario
        SET stock_actual = stock_actual - p_cantidad,
            fecha_actualizacion = CURRENT_TIMESTAMP
        WHERE id_inventario = v_id_inventario;

    END IF;

    -- Registrar observaciones conservando la dirección
    v_observaciones :=
        CONCAT(
            'Dirección: ',
            p_direccion,
            CASE
                WHEN NULLIF(TRIM(p_observaciones), '') IS NOT NULL
                THEN E'\n' || TRIM(p_observaciones)
                ELSE ''
            END
        );

    -- Registrar el movimiento con el tipo correcto
    INSERT INTO movimiento_inventario (
        inventario_id,
        tipo_movimiento,
        cantidad,
        fecha,
        referencia,
        observaciones,
        usuario_id
    )
    VALUES (
        v_id_inventario,
        v_tipo_movimiento,
        p_cantidad,
        CURRENT_TIMESTAMP,
        p_referencia,
        v_observaciones,
        p_usuario_id
    );

    o_resultado := 'OK';

EXCEPTION
    WHEN OTHERS THEN
        o_resultado := 'Error: ' || SQLERRM;
END;
$BODY$;
----------------------------------------------------------

CREATE OR REPLACE PROCEDURE public.sp_contar_movimientos_inventario(
	IN p_id_producto integer,
	IN p_tipo_movimiento character varying,
	IN p_fecha_desde date,
	IN p_fecha_hasta date,
	OUT o_total integer)
LANGUAGE 'plpgsql'
AS $BODY$

BEGIN
    IF p_fecha_desde IS NOT NULL
       AND p_fecha_hasta IS NOT NULL
       AND p_fecha_desde > p_fecha_hasta THEN
        RAISE EXCEPTION 'La fecha inicial no puede ser posterior a la fecha final.';
    END IF;

    SELECT COUNT(*)::INTEGER
    INTO o_total
    FROM movimiento_inventario mi
    INNER JOIN inventario i
        ON i.id_inventario = mi.inventario_id
    INNER JOIN producto p
        ON p.id_producto = i.producto_id
    WHERE
        (p_id_producto IS NULL
            OR p.id_producto = p_id_producto)
        AND (
            p_tipo_movimiento IS NULL
            OR UPPER(TRIM(p_tipo_movimiento)) = 'TODOS'
            OR mi.tipo_movimiento =
                UPPER(TRIM(p_tipo_movimiento))
        )
        AND (
            p_fecha_desde IS NULL
            OR mi.fecha >= p_fecha_desde
        )
        AND (
            p_fecha_hasta IS NULL
            OR mi.fecha < p_fecha_hasta + 1
        );
END;
$BODY$;
----------------------------------------------------------

CREATE OR REPLACE PROCEDURE public.sp_listar_movimientos_inventario(
	IN p_id_producto integer,
	IN p_tipo_movimiento character varying,
	IN p_fecha_desde date,
	IN p_fecha_hasta date,
	IN p_pagina integer,
	IN p_tamano_pagina integer,
	OUT o_movimientos json)
LANGUAGE 'plpgsql'
AS $BODY$

DECLARE
    v_offset INTEGER;
BEGIN
    -- Validar los parámetros de paginación
    IF p_pagina IS NULL OR p_pagina < 1 THEN
        RAISE EXCEPTION 'El número de página debe ser mayor que cero.';
    END IF;

    IF p_tamano_pagina IS NULL OR p_tamano_pagina < 1 THEN
        RAISE EXCEPTION 'El tamaño de página debe ser mayor que cero.';
    END IF;

    IF p_fecha_desde IS NOT NULL
       AND p_fecha_hasta IS NOT NULL
       AND p_fecha_desde > p_fecha_hasta THEN
        RAISE EXCEPTION 'La fecha inicial no puede ser posterior a la fecha final.';
    END IF;

    v_offset := (p_pagina - 1) * p_tamano_pagina;

    SELECT COALESCE(
        JSON_AGG(
            fila.movimiento
            ORDER BY fila.fecha DESC, fila.id_movimiento DESC
        ),
        '[]'::JSON
    )
    INTO o_movimientos
    FROM (
        SELECT
            mi.id_movimiento,
            mi.fecha,
            JSON_BUILD_OBJECT(
                'IdMovimiento', mi.id_movimiento,
                'IdProducto', p.id_producto,
                'IdInventario', i.id_inventario,
                'Codigo', p.codigo,
                'NombreProducto', p.nombre,
                'TipoMovimiento', mi.tipo_movimiento,
                'Cantidad', mi.cantidad,
                'Fecha', mi.fecha,
                'Referencia', mi.referencia,
                'Observaciones', mi.observaciones,
                'IdUsuario', u.id_usuario,
                'NombreUsuario', u.nombre
            ) AS movimiento
        FROM movimiento_inventario mi
        INNER JOIN inventario i
            ON i.id_inventario = mi.inventario_id
        INNER JOIN producto p
            ON p.id_producto = i.producto_id
        INNER JOIN usuario u
            ON u.id_usuario = mi.usuario_id
        WHERE
            (p_id_producto IS NULL
                OR p.id_producto = p_id_producto)
            AND (
                p_tipo_movimiento IS NULL
                OR UPPER(TRIM(p_tipo_movimiento)) = 'TODOS'
                OR mi.tipo_movimiento =
                    UPPER(TRIM(p_tipo_movimiento))
            )
            AND (
                p_fecha_desde IS NULL
                OR mi.fecha >= p_fecha_desde
            )
            AND (
                p_fecha_hasta IS NULL
                OR mi.fecha < p_fecha_hasta + 1
            )
        ORDER BY mi.fecha DESC, mi.id_movimiento DESC
        LIMIT p_tamano_pagina
        OFFSET v_offset
    ) AS fila;
END;
$BODY$;
----------------------------------------------------------

CREATE OR REPLACE PROCEDURE public.sp_registrar_entrada_inventario(
	IN p_id_producto integer,
	IN p_cantidad numeric,
	IN p_referencia character varying,
	IN p_observaciones text,
	IN p_usuario_id integer,
	OUT o_resultado character varying)
LANGUAGE 'plpgsql'
AS $BODY$

DECLARE
    v_id_inventario INTEGER;
BEGIN
    o_resultado := 'ERROR';

    -- Validar cantidad
    IF p_cantidad IS NULL OR p_cantidad <= 0 THEN
        o_resultado := 'CANTIDAD_INVALIDA';
        RETURN;
    END IF;

    -- Validar usuario activo
    IF NOT EXISTS (
        SELECT 1
        FROM usuario
        WHERE id_usuario = p_usuario_id
          AND estado = TRUE
    ) THEN
        o_resultado := 'USUARIO_INVALIDO';
        RETURN;
    END IF;

    -- Validar que el producto exista y esté activo
    IF NOT EXISTS (
        SELECT 1
        FROM producto
        WHERE id_producto = p_id_producto
          AND estado = TRUE
    ) THEN
        o_resultado := 'PRODUCTO_NO_ENCONTRADO';
        RETURN;
    END IF;

    -- Crear el registro de inventario si todavía no existe.
    -- Si ya existe, no modifica el stock en este paso.
    INSERT INTO inventario (
        producto_id,
        stock_actual,
        stock_minimo,
        fecha_actualizacion
    )
    VALUES (
        p_id_producto,
        0,
        0,
        CURRENT_TIMESTAMP
    )
    ON CONFLICT (producto_id) DO NOTHING;

    -- Bloquear el registro para evitar actualizaciones
    -- simultáneas sobre el mismo inventario.
    SELECT id_inventario
    INTO v_id_inventario
    FROM inventario
    WHERE producto_id = p_id_producto
    FOR UPDATE;

    IF v_id_inventario IS NULL THEN
        o_resultado := 'INVENTARIO_NO_ENCONTRADO';
        RETURN;
    END IF;

    -- Actualizar las existencias
    UPDATE inventario
    SET stock_actual = stock_actual + p_cantidad,
        fecha_actualizacion = CURRENT_TIMESTAMP
    WHERE id_inventario = v_id_inventario;

    -- Registrar el movimiento
    INSERT INTO movimiento_inventario (
        inventario_id,
        tipo_movimiento,
        cantidad,
        fecha,
        referencia,
        observaciones,
        usuario_id
    )
    VALUES (
        v_id_inventario,
        'ENTRADA',
        p_cantidad,
        CURRENT_TIMESTAMP,
        p_referencia,
        p_observaciones,
        p_usuario_id
    );

    o_resultado := 'REGISTRADO';
END;
$BODY$;
----------------------------------------------------------

CREATE OR REPLACE PROCEDURE public.sp_obtener_productos_para_inventario(
	OUT o_productos json)
LANGUAGE 'plpgsql'
AS $BODY$

BEGIN
    SELECT COALESCE(
        json_agg(
            json_build_object(
                'IdProducto', p.id_producto,
                'Codigo', p.codigo,
                'Nombre', p.nombre
            )
            ORDER BY p.nombre
        ),
        '[]'::JSON
    )
    INTO o_productos
    FROM producto p
    WHERE p.estado = TRUE;
END;
$BODY$;
----------------------------------------------------------

CREATE OR REPLACE PROCEDURE public.sp_obtener_productos_stock_bajo(
	OUT o_productos json)
LANGUAGE 'plpgsql'
AS $BODY$

BEGIN
    SELECT COALESCE(
        json_agg(
            json_build_object(
                'IdProducto', p.id_producto,
                'IdInventario', i.id_inventario,
                'Codigo', p.codigo,
                'Nombre', p.nombre,
                'Categoria', p.categoria,
                'PrecioVenta', p.precio_venta,
                'StockActual', COALESCE(i.stock_actual, 0),
                'StockMinimo', COALESCE(i.stock_minimo, 0),
                'StockMaximo', i.stock_maximo,
                'Ubicacion', i.ubicacion,
                'FechaActualizacion', i.fecha_actualizacion
            )
            ORDER BY p.nombre
        ),
        '[]'::JSON
    )
    INTO o_productos
    FROM producto p
    LEFT JOIN inventario i
        ON i.producto_id = p.id_producto
    WHERE p.estado = TRUE
      AND COALESCE(i.stock_actual, 0)
          <= COALESCE(i.stock_minimo, 0)
      AND COALESCE(i.stock_actual, 0) > 0;
END;
$BODY$;
---------------------------------------------------------

CREATE OR REPLACE PROCEDURE public.sp_obtener_resumen_inventario(
	OUT o_total_productos integer,
	OUT o_stock_bajo integer,
	OUT o_agotados integer)
LANGUAGE 'plpgsql'
AS $BODY$

BEGIN
    SELECT
        COUNT(*)::INTEGER,
        COUNT(*) FILTER (
            WHERE COALESCE(i.stock_actual, 0)
                  <= COALESCE(i.stock_minimo, 0)
              AND COALESCE(i.stock_actual, 0) > 0
        )::INTEGER,
        COUNT(*) FILTER (
            WHERE COALESCE(i.stock_actual, 0) = 0
        )::INTEGER
    INTO
        o_total_productos,
        o_stock_bajo,
        o_agotados
    FROM producto p
    LEFT JOIN inventario i
        ON i.producto_id = p.id_producto
    WHERE p.estado = TRUE;
END;
$BODY$;
--------------------------------------------------------

CREATE OR REPLACE PROCEDURE public.sp_listar_inventario(
	IN p_busqueda character varying,
	IN p_estado character varying,
	OUT o_productos json)
LANGUAGE 'plpgsql'
AS $BODY$

BEGIN
    SELECT COALESCE(
        json_agg(
            json_build_object(
                'IdProducto', p.id_producto,
                'IdInventario', i.id_inventario,
                'Codigo', p.codigo,
                'Nombre', p.nombre,
                'Categoria', p.categoria,
                'PrecioVenta', p.precio_venta,
                'StockActual', COALESCE(i.stock_actual, 0),
                'StockMinimo', COALESCE(i.stock_minimo, 0),
                'StockMaximo', i.stock_maximo,
                'Ubicacion', i.ubicacion,
                'FechaActualizacion', i.fecha_actualizacion
            )
            ORDER BY p.nombre
        ),
        '[]'::JSON
    )
    INTO o_productos
    FROM producto p
    LEFT JOIN inventario i
        ON i.producto_id = p.id_producto
    WHERE p.estado = TRUE
      AND (
          NULLIF(TRIM(p_busqueda), '') IS NULL
          OR p.codigo ILIKE '%' || TRIM(p_busqueda) || '%'
          OR p.nombre ILIKE '%' || TRIM(p_busqueda) || '%'
      )
      AND (
          NULLIF(TRIM(p_estado), '') IS NULL
          OR UPPER(TRIM(p_estado)) = 'TODOS'
          OR (
              UPPER(TRIM(p_estado)) = 'BAJO'
              AND COALESCE(i.stock_actual, 0)
                  <= COALESCE(i.stock_minimo, 0)
              AND COALESCE(i.stock_actual, 0) > 0
          )
          OR (
              UPPER(TRIM(p_estado)) = 'AGOTADO'
              AND COALESCE(i.stock_actual, 0) = 0
          )
          OR (
              UPPER(TRIM(p_estado)) = 'NORMAL'
              AND COALESCE(i.stock_actual, 0)
                  > COALESCE(i.stock_minimo, 0)
          )
      );
END;
$BODY$;
