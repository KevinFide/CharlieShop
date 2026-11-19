-- ============================================================
-- Charlie Shop
-- Procedimiento de detalle para comprobante por correo.
-- Corrige también ventas históricas cuyos campos de descuento
-- e impuesto en detalle_venta quedaron guardados en cero.
-- La distribución proporcional cuadra al centavo con la cabecera.
-- ============================================================

CREATE OR REPLACE PROCEDURE public.sp_obtener_detalle_venta_correo(
    IN p_id_venta INTEGER,
    INOUT o_detalle TEXT
)
LANGUAGE plpgsql
AS $BODY$
BEGIN
    SELECT
        CASE
            WHEN v.id_venta IS NULL THEN NULL
            ELSE JSON_BUILD_OBJECT(
                'IdVenta', v.id_venta,
                'Fecha', v.fecha,
                'NombreCliente', CONCAT_WS(' ', c.nombre, c.apellido),
                'Email', c.email,
                'FormaPago', v.forma_pago,
                'Estado', v.estado,
                'Subtotal', v.total,
                'Descuento', v.descuento,
                'Impuesto', v.impuesto,
                'TotalNeto', v.total_neto,
                'SaldoPendiente',
                    CASE
                        WHEN UPPER(v.forma_pago) = 'CREDITO'
                            THEN COALESCE(cxc.saldo_pendiente, v.total_neto)
                        ELSE 0
                    END,
                'FechaVencimiento', cxc.fecha_vencimiento,
                'Productos', COALESCE((
                    WITH lineas AS (
                        SELECT
                            dv.id_detalle_venta,
                            dv.producto_id,
                            dv.cantidad,
                            dv.precio_unitario,
                            dv.descuento,
                            dv.impuesto,
                            dv.subtotal,
                            p.codigo,
                            p.nombre,
                            ROW_NUMBER() OVER (
                                ORDER BY dv.id_detalle_venta
                            ) AS rn,
                            COUNT(*) OVER () AS total_lineas,
                            SUM(dv.descuento) OVER () AS descuento_guardado,
                            SUM(dv.impuesto) OVER () AS impuesto_guardado,
                            SUM(dv.subtotal) OVER () AS subtotal_lineas
                        FROM public.detalle_venta dv
                        LEFT JOIN public.producto p
                            ON p.id_producto = dv.producto_id
                        WHERE dv.venta_id = v.id_venta
                    ),
                    descuento_base AS (
                        SELECT
                            l.*,
                            CASE
                                WHEN COALESCE(l.subtotal_lineas, 0) > 0
                                THEN ROUND(
                                    COALESCE(v.descuento, 0)
                                    * l.subtotal / l.subtotal_lineas,
                                    2
                                )
                                ELSE 0
                            END AS descuento_proporcional
                        FROM lineas l
                    ),
                    descuentos AS (
                        SELECT
                            d.*,
                            CASE
                                WHEN COALESCE(d.descuento_guardado, 0) > 0
                                    THEN d.descuento
                                WHEN d.rn = d.total_lineas
                                    THEN ROUND(
                                        COALESCE(v.descuento, 0)
                                        - COALESCE(
                                            SUM(d.descuento_proporcional) OVER (
                                                ORDER BY d.rn
                                                ROWS BETWEEN UNBOUNDED PRECEDING
                                                AND 1 PRECEDING
                                            ),
                                            0
                                        ),
                                        2
                                    )
                                ELSE d.descuento_proporcional
                            END AS descuento_correo
                        FROM descuento_base d
                    ),
                    bases AS (
                        SELECT
                            d.*,
                            GREATEST(
                                0,
                                d.subtotal - d.descuento_correo
                            ) AS base_producto,
                            SUM(
                                GREATEST(
                                    0,
                                    d.subtotal - d.descuento_correo
                                )
                            ) OVER () AS base_total
                        FROM descuentos d
                    ),
                    impuesto_base AS (
                        SELECT
                            b.*,
                            CASE
                                WHEN COALESCE(b.base_total, 0) > 0
                                THEN ROUND(
                                    COALESCE(v.impuesto, 0)
                                    * b.base_producto / b.base_total,
                                    2
                                )
                                ELSE 0
                            END AS impuesto_proporcional
                        FROM bases b
                    ),
                    impuestos AS (
                        SELECT
                            i.*,
                            CASE
                                WHEN COALESCE(i.impuesto_guardado, 0) > 0
                                    THEN i.impuesto
                                WHEN i.rn = i.total_lineas
                                    THEN ROUND(
                                        COALESCE(v.impuesto, 0)
                                        - COALESCE(
                                            SUM(i.impuesto_proporcional) OVER (
                                                ORDER BY i.rn
                                                ROWS BETWEEN UNBOUNDED PRECEDING
                                                AND 1 PRECEDING
                                            ),
                                            0
                                        ),
                                        2
                                    )
                                ELSE i.impuesto_proporcional
                            END AS impuesto_correo
                        FROM impuesto_base i
                    )
                    SELECT JSON_AGG(
                        JSON_BUILD_OBJECT(
                            'IdDetalleVenta', i.id_detalle_venta,
                            'IdProducto', i.producto_id,
                            'CodigoProducto', i.codigo,
                            'NombreProducto', i.nombre,
                            'Cantidad', i.cantidad,
                            'PrecioUnitario', i.precio_unitario,
                            'Descuento', i.descuento_correo,
                            'Impuesto', i.impuesto_correo,
                            'Subtotal', i.subtotal
                        )
                        ORDER BY i.id_detalle_venta
                    )
                    FROM impuestos i
                ), '[]'::JSON)
            )::TEXT
        END
    INTO o_detalle
    FROM public.venta v
    LEFT JOIN public.cliente c
        ON c.id_cliente = v.cliente_id
    LEFT JOIN public.cuenta_por_cobrar cxc
        ON cxc.venta_id = v.id_venta
    WHERE v.id_venta = p_id_venta;
END;
$BODY$;

ALTER PROCEDURE public.sp_obtener_detalle_venta_correo(INTEGER, TEXT)
    OWNER TO postgres;
