
CREATE OR REPLACE PROCEDURE public.sp_obtener_productos(
    IN p_busqueda character varying,
    IN p_categoria character varying,
    INOUT o_productos text
)
LANGUAGE plpgsql
AS $procedure$
BEGIN

    SELECT COALESCE(
        json_agg(
            json_build_object(
                'IdProducto', p.id_producto,
                'Codigo', p.codigo,
                'Nombre', p.nombre,
                'Descripcion', p.descripcion,
                'Categoria', p.categoria,
                'PrecioVenta', p.precio_venta,
                'Costo', p.costo,
                'UnidadMedida', p.unidad_medida,
                'Estado', p.estado,
                'FechaCreacion', p.fecha_creacion,
                'StockActual', COALESCE(i.stock_actual, 0),
                'StockMinimo', COALESCE(i.stock_minimo, 0)
            )
            ORDER BY p.id_producto
        ),
        '[]'
    )::TEXT
    INTO o_productos
    FROM producto p
    LEFT JOIN inventario i
        ON i.producto_id = p.id_producto
    WHERE
        (
            p_busqueda IS NULL
            OR TRIM(p_busqueda) = ''
            OR p.nombre ILIKE '%' || TRIM(p_busqueda) || '%'
        )
        AND
        (
            p_categoria IS NULL
            OR TRIM(p_categoria) = ''
            OR p.categoria = TRIM(p_categoria)
        );

END;
$procedure$;