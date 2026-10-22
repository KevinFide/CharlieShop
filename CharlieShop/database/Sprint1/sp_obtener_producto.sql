
CREATE OR REPLACE PROCEDURE public.sp_obtener_producto(
    IN p_id_producto integer,
    INOUT o_producto text
)
LANGUAGE plpgsql
AS $procedure$
BEGIN

    SELECT COALESCE(
        (
            SELECT json_build_object(
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
            )::TEXT
            FROM producto p
            LEFT JOIN inventario i
                ON i.producto_id = p.id_producto
            WHERE p.id_producto = p_id_producto
        ),
        '{}'
    )
    INTO o_producto;

END;
$procedure$;