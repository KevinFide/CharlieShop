-- Nuevo overload para registrar producto + inventario inicial.
-- Mantiene intacta la versión anterior de sp_crear_producto durante la migración.

CREATE OR REPLACE PROCEDURE public.sp_crear_producto(
    IN p_codigo character varying,
    IN p_nombre character varying,
    IN p_descripcion text,
    IN p_categoria character varying,
    IN p_precio_venta numeric,
    IN p_costo numeric,
    IN p_unidad_medida character varying,
    IN p_stock_inicial numeric,
    IN p_usuario_id integer,
    INOUT o_resultado text
)
LANGUAGE plpgsql
AS $procedure$
DECLARE
    v_id_producto integer;
    v_resultado_movimiento varchar(100);
BEGIN
    o_resultado := NULL;

    IF EXISTS (
        SELECT 1 FROM producto WHERE codigo = TRIM(p_codigo)
    ) THEN
        o_resultado := 'CODIGO_EXISTENTE';
        RETURN;
    END IF;

    IF p_stock_inicial IS NULL OR p_stock_inicial < 0 THEN
        o_resultado := 'STOCK_INVALIDO';
        RETURN;
    END IF;

    IF p_stock_inicial > 0 AND p_usuario_id IS NULL THEN
        o_resultado := 'USUARIO_INVALIDO';
        RETURN;
    END IF;

    INSERT INTO producto (
        codigo, nombre, descripcion, categoria,
        precio_venta, costo, unidad_medida
    )
    VALUES (
        TRIM(p_codigo), TRIM(p_nombre), p_descripcion, p_categoria,
        p_precio_venta, p_costo, p_unidad_medida
    )
    RETURNING id_producto INTO v_id_producto;

    -- Siempre crear inventario, incluso cuando el stock inicial sea cero.
    INSERT INTO inventario (
        producto_id, stock_actual, stock_minimo, fecha_actualizacion
    )
    VALUES (
        v_id_producto, 0, 0, CURRENT_TIMESTAMP
    );

    -- Para cantidades positivas, reutilizar la lógica de entradas existente.
    IF p_stock_inicial > 0 THEN
        CALL public.sp_ajustar_inventario(
            v_id_producto,
            'ENTRADA',
            p_stock_inicial,
            'STOCK_INICIAL',
            'Existencia inicial al registrar el producto.',
            p_usuario_id,
            v_resultado_movimiento
        );

        IF v_resultado_movimiento IS DISTINCT FROM 'OK' THEN
            RAISE EXCEPTION 'No se pudo registrar el stock inicial: %',
                COALESCE(v_resultado_movimiento, 'resultado vacío');
        END IF;
    END IF;

    o_resultado := 'CREADO';
END;
$procedure$;
