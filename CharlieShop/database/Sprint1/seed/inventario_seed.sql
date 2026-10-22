-- ============================================================
-- CHARLIE SHOP
-- Datos de prueba - Inventario
-- ============================================================
-- Datos ficticios utilizados para desarrollo y pruebas.
-- NO corresponden a información real del negocio.
-- ============================================================

INSERT INTO inventario (
    producto_id,
    stock_actual,
    stock_minimo,
    stock_maximo,
    ubicacion
)
SELECT
    p.id_producto,
    datos.stock_actual,
    datos.stock_minimo,
    datos.stock_maximo,
    datos.ubicacion
FROM (
    VALUES
        ('P-001', 24.00, 10.00, 50.00, 'Bodega A'),
        ('P-002', 12.00, 8.00, 30.00, 'Bodega A'),
        ('P-003', 7.00, 5.00, 20.00, 'Bodega A'),
        ('P-004', 4.00, 5.00, 20.00, 'Bodega A'),
        ('P-005', 8.00, 5.00, 25.00, 'Bodega B'),
        ('P-006', 11.00, 6.00, 25.00, 'Bodega B'),
        ('P-007', 3.00, 5.00, 20.00, 'Bodega B'),
        ('P-008', 6.00, 5.00, 15.00, 'Bodega C'),
        ('P-009', 2.00, 5.00, 15.00, 'Bodega C'),
        ('P-010', 5.00, 5.00, 15.00, 'Bodega C'),
        ('P-011', 7.00, 5.00, 15.00, 'Bodega C'),
        ('P-012', 16.00, 8.00, 30.00, 'Bodega D'),
        ('P-013', 8.00, 5.00, 20.00, 'Bodega D'),
        ('P-014', 5.00, 3.00, 15.00, 'Bodega D'),
        ('P-015', 9.00, 5.00, 15.00, 'Bodega D')
) AS datos(
    codigo,
    stock_actual,
    stock_minimo,
    stock_maximo,
    ubicacion
)
INNER JOIN producto p
    ON p.codigo = datos.codigo;