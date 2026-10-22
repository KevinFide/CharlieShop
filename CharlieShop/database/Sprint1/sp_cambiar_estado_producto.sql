
CREATE OR REPLACE PROCEDURE public.sp_cambiar_estado_producto(
    IN p_id_producto integer,
    IN p_estado boolean,
    INOUT o_resultado text
)
LANGUAGE plpgsql
AS $procedure$
BEGIN

    -- Verificar que el producto exista
    IF NOT EXISTS (
        SELECT 1
        FROM producto
        WHERE id_producto = p_id_producto
    ) THEN

        o_resultado := 'PRODUCTO_NO_ENCONTRADO';
        RETURN;

    END IF;


    -- Actualizar el estado del producto
    UPDATE producto
    SET estado = p_estado
    WHERE id_producto = p_id_producto;


    -- Resultado de la operación
    IF p_estado = TRUE THEN

        o_resultado := 'ACTIVADO';

    ELSE

        o_resultado := 'DESACTIVADO';

    END IF;

END;
$procedure$;