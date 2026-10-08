CREATE OR REPLACE PROCEDURE public.sp_eliminar_producto(
    IN p_id_producto INTEGER,
    INOUT o_resultado TEXT
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
        o_resultado := 'NO_ENCONTRADO';
        RETURN;
    END IF;

    -- Verificar si tiene movimientos de inventario
    IF EXISTS (
        SELECT 1
        FROM inventario i
        INNER JOIN movimiento_inventario mi
            ON mi.inventario_id = i.id_inventario
        WHERE i.producto_id = p_id_producto
    ) THEN
        o_resultado := 'TIENE_MOVIMIENTOS';
        RETURN;
    END IF;

    -- Verificar si ha sido utilizado en ventas
    IF EXISTS (
        SELECT 1
        FROM detalle_venta
        WHERE producto_id = p_id_producto
    ) THEN
        o_resultado := 'TIENE_VENTAS';
        RETURN;
    END IF;

    -- Eliminar inventario asociado
    DELETE FROM inventario
    WHERE producto_id = p_id_producto;

    -- Eliminar producto
    DELETE FROM producto
    WHERE id_producto = p_id_producto;

    o_resultado := 'ELIMINADO';

END;
$procedure$;