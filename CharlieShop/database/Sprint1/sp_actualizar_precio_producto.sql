
CREATE OR REPLACE PROCEDURE public.sp_actualizar_precio_producto(
    IN p_id_producto integer,
    IN p_precio_venta numeric,
    INOUT o_resultado text
)
LANGUAGE plpgsql
AS $procedure$
BEGIN

    -- Validar que el precio sea mayor que cero
    IF p_precio_venta IS NULL OR p_precio_venta <= 0 THEN
        o_resultado := 'PRECIO_INVALIDO';
        RETURN;
    END IF;

    -- Verificar que el producto exista
    IF NOT EXISTS (
        SELECT 1
        FROM producto
        WHERE id_producto = p_id_producto
    ) THEN
        o_resultado := 'PRODUCTO_NO_ENCONTRADO';
        RETURN;
    END IF;

    -- Actualizar precio
    UPDATE producto
    SET precio_venta = p_precio_venta
    WHERE id_producto = p_id_producto;

    o_resultado := 'ACTUALIZADO';

END;
$procedure$;