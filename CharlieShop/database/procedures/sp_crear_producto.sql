CREATE OR REPLACE PROCEDURE public.sp_crear_producto(
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

    IF EXISTS (
        SELECT 1
        FROM producto
        WHERE codigo = TRIM(p_codigo)
    ) THEN
        o_resultado := 'CODIGO_EXISTENTE';
        RETURN;
    END IF;

    INSERT INTO producto (
        codigo,
        nombre,
        descripcion,
        categoria,
        precio_venta,
        costo,
        unidad_medida
    )
    VALUES (
        TRIM(p_codigo),
        TRIM(p_nombre),
        p_descripcion,
        p_categoria,
        p_precio_venta,
        p_costo,
        p_unidad_medida
    );

    o_resultado := 'CREADO';

END;
$procedure$;