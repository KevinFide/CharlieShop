CREATE OR REPLACE PROCEDURE public.sp_editar_producto(
    IN p_id_producto INTEGER,
    IN p_codigo VARCHAR(50),
    IN p_nombre VARCHAR(150),
    IN p_descripcion TEXT,
    IN p_categoria VARCHAR(100),
    IN p_precio_venta NUMERIC(12,2),
    IN p_costo NUMERIC(12,2),
    IN p_unidad_medida VARCHAR(20),
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

    -- Verificar que el código no pertenezca a otro producto
    IF EXISTS (
        SELECT 1
        FROM producto
        WHERE codigo = TRIM(p_codigo)
          AND id_producto <> p_id_producto
    ) THEN
        o_resultado := 'CODIGO_EXISTENTE';
        RETURN;
    END IF;

    UPDATE producto
    SET
        codigo = TRIM(p_codigo),
        nombre = TRIM(p_nombre),
        descripcion = p_descripcion,
        categoria = p_categoria,
        precio_venta = p_precio_venta,
        costo = p_costo,
        unidad_medida = p_unidad_medida
    WHERE id_producto = p_id_producto;

    o_resultado := 'ACTUALIZADO';

END;
$procedure$;