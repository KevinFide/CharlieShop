CREATE OR REPLACE PROCEDURE sp_consultar_historial_ventas(
    OUT o_ventas JSON
)
LANGUAGE plpgsql
AS $$
BEGIN
    SELECT COALESCE(
        JSON_AGG(
            JSON_BUILD_OBJECT(
                'IdVenta', v.id_venta,
                'Fecha', v.fecha,
                'NombreCliente',
                    NULLIF(
                        BTRIM(
                            CONCAT_WS(
                                ' ',
                                c.nombre,
                                c.apellido
                            )
                        ),
                        ''
                    ),
                'NombreVendedor', u.nombre,
                'Total', v.total,
                'Descuento', v.descuento,
                'Impuesto', v.impuesto,
                'TotalNeto', v.total_neto,
                'FormaPago', v.forma_pago,
                'Estado', v.estado
            )
            ORDER BY v.fecha DESC, v.id_venta DESC
        ),
        '[]'::JSON
    )
    INTO o_ventas
    FROM venta v
    LEFT JOIN cliente c
        ON c.id_cliente = v.cliente_id
    LEFT JOIN usuario u
        ON u.id_usuario = v.usuario_id;
END;
$$;