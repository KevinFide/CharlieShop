-- ============================================================
-- Charlie Shop
-- Corrección de distribución de descuento e impuesto por línea
-- Reemplaza public.sp_registrar_venta(...)
--
-- El descuento y el IVA se distribuyen proporcionalmente entre
-- los productos. La última línea absorbe cualquier diferencia
-- de redondeo para que la suma coincida exactamente con la venta.
-- ============================================================

CREATE OR REPLACE PROCEDURE public.sp_registrar_venta(
    IN p_cliente_id integer,
    IN p_forma_pago character varying,
    IN p_usuario_id integer,
    IN p_descuento_porcentaje numeric,
    IN p_productos json,
    OUT o_resultado character varying,
    OUT o_id_venta integer
)
LANGUAGE plpgsql
AS $BODY$
DECLARE
    v_item JSON;
    v_id_producto INTEGER;
    v_cantidad NUMERIC(12,2);
    v_precio NUMERIC(12,2);
    v_stock NUMERIC(12,2);
    v_id_inventario INTEGER;
    v_subtotal NUMERIC(12,2);
    v_total NUMERIC(12,2) := 0;
    v_descuento NUMERIC(12,2) := 0;
    v_base_imponible NUMERIC(12,2) := 0;
    v_impuesto NUMERIC(12,2) := 0;
    v_total_neto NUMERIC(12,2) := 0;
    v_id_venta INTEGER;
    v_estado_venta VARCHAR(20);

    -- Variables para distribuir descuento e impuesto por producto.
    v_total_productos INTEGER := 0;
    v_indice INTEGER := 0;
    v_descuento_producto NUMERIC(12,2) := 0;
    v_impuesto_producto NUMERIC(12,2) := 0;
    v_base_producto NUMERIC(12,2) := 0;
    v_descuento_acumulado NUMERIC(12,2) := 0;
    v_impuesto_acumulado NUMERIC(12,2) := 0;
BEGIN
    o_resultado := NULL;
    o_id_venta := NULL;

    -- 1. Validar el carrito.
    IF p_productos IS NULL
       OR JSON_TYPEOF(p_productos) IS DISTINCT FROM 'array'
       OR JSON_ARRAY_LENGTH(p_productos) = 0 THEN
        RAISE EXCEPTION 'Debe agregar al menos un producto a la venta.';
    END IF;

    -- 2. Validar la forma de pago.
    IF p_forma_pago IS NULL
       OR p_forma_pago NOT IN ('CONTADO', 'CREDITO') THEN
        RAISE EXCEPTION 'La forma de pago debe ser CONTADO o CREDITO.';
    END IF;

    -- 3. Validar el porcentaje de descuento.
    IF p_descuento_porcentaje IS NULL
       OR p_descuento_porcentaje < 0
       OR p_descuento_porcentaje > 100 THEN
        RAISE EXCEPTION 'El descuento debe estar entre 0 y 100 por ciento.';
    END IF;

    -- 4. Validar el usuario.
    IF NOT EXISTS (
        SELECT 1
        FROM usuario u
        WHERE u.id_usuario = p_usuario_id
          AND u.estado = TRUE
    ) THEN
        RAISE EXCEPTION 'El usuario no existe o está inactivo.';
    END IF;

    -- 5. Validar cliente, si se proporcionó.
    IF p_cliente_id IS NOT NULL
       AND NOT EXISTS (
           SELECT 1
           FROM cliente c
           WHERE c.id_cliente = p_cliente_id
             AND c.estado = TRUE
       ) THEN
        RAISE EXCEPTION 'El cliente no existe o está inactivo.';
    END IF;

    -- 6. Validar estructura, cantidades e identificadores.
    FOR v_item IN
        SELECT value
        FROM JSON_ARRAY_ELEMENTS(p_productos)
    LOOP
        BEGIN
            v_id_producto := NULLIF(v_item ->> 'IdProducto', '')::INTEGER;
            v_cantidad := NULLIF(v_item ->> 'Cantidad', '')::NUMERIC;
        EXCEPTION
            WHEN OTHERS THEN
                RAISE EXCEPTION 'El detalle contiene un producto o una cantidad inválidos.';
        END;

        IF v_id_producto IS NULL
           OR v_id_producto <= 0
           OR v_cantidad IS NULL
           OR v_cantidad <= 0
           OR v_cantidad = 'NaN'::NUMERIC
           OR v_cantidad IN ('Infinity'::NUMERIC, '-Infinity'::NUMERIC) THEN
            RAISE EXCEPTION
                'Cada producto debe tener un ID válido y una cantidad finita mayor que cero.';
        END IF;
    END LOOP;

    -- 7. Evitar productos repetidos en el JSON.
    IF EXISTS (
        SELECT 1
        FROM JSON_TO_RECORDSET(p_productos)
            AS x("IdProducto" INTEGER, "Cantidad" NUMERIC)
        GROUP BY x."IdProducto"
        HAVING COUNT(*) > 1
    ) THEN
        RAISE EXCEPTION 'Hay productos repetidos en el detalle de la venta.';
    END IF;

    -- 8. Bloquear inventarios en orden de ID y validar existencias.
    FOR v_id_producto IN
        SELECT x."IdProducto"
        FROM JSON_TO_RECORDSET(p_productos)
            AS x("IdProducto" INTEGER, "Cantidad" NUMERIC)
        ORDER BY x."IdProducto"
    LOOP
        IF NOT EXISTS (
            SELECT 1
            FROM producto p
            WHERE p.id_producto = v_id_producto
              AND p.estado = TRUE
        ) THEN
            RAISE EXCEPTION
                'El producto con ID % no existe o está inactivo.',
                v_id_producto;
        END IF;

        SELECT i.id_inventario, i.stock_actual
        INTO v_id_inventario, v_stock
        FROM inventario i
        WHERE i.producto_id = v_id_producto
        FOR UPDATE;

        IF NOT FOUND THEN
            RAISE EXCEPTION
                'El producto con ID % no tiene registro de inventario.',
                v_id_producto;
        END IF;

        SELECT (x.value ->> 'Cantidad')::NUMERIC
        INTO v_cantidad
        FROM JSON_ARRAY_ELEMENTS(p_productos) AS x(value)
        WHERE (x.value ->> 'IdProducto')::INTEGER = v_id_producto;

        IF v_stock < v_cantidad THEN
            RAISE EXCEPTION
                'Stock insuficiente para el producto ID %. Disponible: %, solicitado: %.',
                v_id_producto, v_stock, v_cantidad;
        END IF;
    END LOOP;

    -- 9. Calcular subtotal con precios vigentes de PostgreSQL.
    FOR v_item IN
        SELECT value
        FROM JSON_ARRAY_ELEMENTS(p_productos)
    LOOP
        v_id_producto := (v_item ->> 'IdProducto')::INTEGER;
        v_cantidad := (v_item ->> 'Cantidad')::NUMERIC;

        SELECT p.precio_venta
        INTO v_precio
        FROM producto p
        WHERE p.id_producto = v_id_producto
          AND p.estado = TRUE;

        v_subtotal := ROUND(v_cantidad * v_precio, 2);
        v_total := v_total + v_subtotal;
    END LOOP;

    v_total := ROUND(v_total, 2);

    -- 10. Calcular descuento, IVA del 13 % y total final.
    v_descuento := ROUND(v_total * p_descuento_porcentaje / 100, 2);

    v_base_imponible := GREATEST(0, v_total - v_descuento);

    v_impuesto := ROUND(v_base_imponible * 0.13, 2);

    v_total_neto := ROUND(v_base_imponible + v_impuesto, 2);

    -- 11. Determinar estado según la forma de pago.
    IF p_forma_pago = 'CONTADO' THEN
        v_estado_venta := 'FINALIZADA';
    ELSE
        v_estado_venta := 'EN_PROCESO';
    END IF;

    -- 12. Registrar cabecera.
    INSERT INTO venta (
        cliente_id,
        total,
        descuento,
        impuesto,
        total_neto,
        forma_pago,
        estado,
        usuario_id
    )
    VALUES (
        p_cliente_id,
        v_total,
        v_descuento,
        v_impuesto,
        v_total_neto,
        p_forma_pago,
        v_estado_venta,
        p_usuario_id
    )
    RETURNING id_venta INTO v_id_venta;

    -- 13. Contar líneas para distribuir los residuos de redondeo.
    SELECT COUNT(*)
    INTO v_total_productos
    FROM JSON_ARRAY_ELEMENTS(p_productos);

    -- 14. Registrar detalle con descuento e impuesto por producto,
    -- descontar stock y guardar movimientos de inventario.
    v_descuento_acumulado := 0;
    v_impuesto_acumulado := 0;
    v_indice := 0;

    FOR v_item IN
        SELECT value
        FROM JSON_ARRAY_ELEMENTS(p_productos)
    LOOP
        v_indice := v_indice + 1;

        v_id_producto := (v_item ->> 'IdProducto')::INTEGER;
        v_cantidad := (v_item ->> 'Cantidad')::NUMERIC;

        SELECT p.precio_venta
        INTO v_precio
        FROM producto p
        WHERE p.id_producto = v_id_producto
          AND p.estado = TRUE;

        v_subtotal := ROUND(v_cantidad * v_precio, 2);

        -- Distribuir descuento proporcionalmente al subtotal de cada línea.
        -- La última línea recibe el residuo para cuadrar exactamente.
        IF v_indice = v_total_productos THEN
            v_descuento_producto :=
                ROUND(v_descuento - v_descuento_acumulado, 2);
        ELSE
            v_descuento_producto :=
                CASE
                    WHEN v_total > 0
                    THEN ROUND(v_descuento * v_subtotal / v_total, 2)
                    ELSE 0
                END;
        END IF;

        v_descuento_producto := GREATEST(0, v_descuento_producto);
        v_base_producto := GREATEST(0, v_subtotal - v_descuento_producto);

        -- Distribuir el IVA sobre la base imponible de cada línea.
        -- La última línea absorbe cualquier diferencia de centavos.
        IF v_indice = v_total_productos THEN
            v_impuesto_producto :=
                ROUND(v_impuesto - v_impuesto_acumulado, 2);
        ELSE
            v_impuesto_producto :=
                CASE
                    WHEN v_base_imponible > 0
                    THEN ROUND(v_impuesto * v_base_producto / v_base_imponible, 2)
                    ELSE 0
                END;
        END IF;

        v_impuesto_producto := GREATEST(0, v_impuesto_producto);

        INSERT INTO detalle_venta (
            venta_id,
            producto_id,
            cantidad,
            precio_unitario,
            descuento,
            impuesto,
            subtotal
        )
        VALUES (
            v_id_venta,
            v_id_producto,
            v_cantidad,
            v_precio,
            v_descuento_producto,
            v_impuesto_producto,
            v_subtotal
        );

        v_descuento_acumulado :=
            v_descuento_acumulado + v_descuento_producto;
        v_impuesto_acumulado :=
            v_impuesto_acumulado + v_impuesto_producto;

        UPDATE inventario
        SET stock_actual = stock_actual - v_cantidad,
            fecha_actualizacion = CURRENT_TIMESTAMP
        WHERE producto_id = v_id_producto
          AND stock_actual >= v_cantidad
        RETURNING id_inventario INTO v_id_inventario;

        IF NOT FOUND THEN
            RAISE EXCEPTION
                'El stock cambió durante la venta del producto ID %.',
                v_id_producto;
        END IF;

        INSERT INTO movimiento_inventario (
            inventario_id,
            tipo_movimiento,
            cantidad,
            referencia,
            observaciones,
            usuario_id
        )
        VALUES (
            v_id_inventario,
            'SALIDA',
            v_cantidad,
            'Venta #' || v_id_venta::VARCHAR,
            'Salida de inventario por venta registrada.',
            p_usuario_id
        );
    END LOOP;

    o_resultado := 'OK';
    o_id_venta := v_id_venta;
END;
$BODY$;

ALTER PROCEDURE public.sp_registrar_venta(
    integer, character varying, integer, numeric, json
) OWNER TO postgres;
