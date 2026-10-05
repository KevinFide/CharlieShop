-- ============================================================
-- CHARLIE SHOP
-- Sistema Integral de Gestión Comercial
-- Script de creación de base de datos - PostgreSQL
-- ============================================================

-- ============================================================
-- PASO 1: ELIMINAR Y CREAR LA BASE DE DATOS
-- ============================================================

-- Ejecutar primero conectado a la base de datos "postgres".
-- ADVERTENCIA: Esto elimina todos los datos existentes.

DROP DATABASE IF EXISTS charlie_shop WITH (FORCE);

CREATE DATABASE charlie_shop;

-- ============================================================
-- PASO 2: CREAR LAS TABLAS Y DEMÁS OBJETOS
-- ============================================================

-- Conectarse a la base de datos "charlie_shop".
-- Ejecutar el resto del script a partir de TABLAS PRINCIPALES.

-- Nota:
-- Los identificadores utilizan INTEGER GENERATED ... AS IDENTITY
-- en lugar de SERIAL, siguiendo el diseño de PostgreSQL.

-- ============================================================
-- TABLAS PRINCIPALES
-- ============================================================

CREATE TABLE rol (
    id_rol INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre VARCHAR(50) NOT NULL UNIQUE,
    descripcion VARCHAR(150),
    estado BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE usuario (
    id_usuario INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre VARCHAR(100) NOT NULL,
    usuario VARCHAR(50) NOT NULL UNIQUE,
    contrasena_hash VARCHAR(255) NOT NULL,
    email VARCHAR(100) UNIQUE,
    rol_id INTEGER NOT NULL,
    estado BOOLEAN NOT NULL DEFAULT TRUE,
    fecha_creacion TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_usuario_rol
        FOREIGN KEY (rol_id)
        REFERENCES rol(id_rol)
);

CREATE TABLE cliente (
    id_cliente INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre VARCHAR(100) NOT NULL,
    apellido VARCHAR(100) NOT NULL,
    identificacion VARCHAR(30) UNIQUE,
    telefono VARCHAR(20) UNIQUE,
    email VARCHAR(100),
    direccion VARCHAR(200),
    fecha_registro TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    estado BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE producto (
    id_producto INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    codigo VARCHAR(50) NOT NULL UNIQUE,
    nombre VARCHAR(150) NOT NULL,
    descripcion TEXT,
    categoria VARCHAR(100),
    precio_venta NUMERIC(12,2) NOT NULL,
    costo NUMERIC(12,2),
    unidad_medida VARCHAR(20),
    estado BOOLEAN NOT NULL DEFAULT TRUE,
    fecha_creacion TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT chk_producto_precio
        CHECK (precio_venta >= 0),

    CONSTRAINT chk_producto_costo
        CHECK (costo IS NULL OR costo >= 0)
);

CREATE TABLE inventario (
    id_inventario INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    producto_id INTEGER NOT NULL UNIQUE,
    stock_actual NUMERIC(12,2) NOT NULL DEFAULT 0,
    stock_minimo NUMERIC(12,2) NOT NULL DEFAULT 0,
    stock_maximo NUMERIC(12,2),
    ubicacion VARCHAR(100),
    fecha_actualizacion TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_inventario_producto
        FOREIGN KEY (producto_id)
        REFERENCES producto(id_producto),

    CONSTRAINT chk_inventario_stock_actual
        CHECK (stock_actual >= 0),

    CONSTRAINT chk_inventario_stock_minimo
        CHECK (stock_minimo >= 0),

    CONSTRAINT chk_inventario_stock_maximo
        CHECK (
            stock_maximo IS NULL
            OR stock_maximo >= stock_minimo
        )
);

CREATE TABLE venta (
    id_venta INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    cliente_id INTEGER,
    fecha TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    total NUMERIC(12,2) NOT NULL DEFAULT 0,
    descuento NUMERIC(12,2) NOT NULL DEFAULT 0,
    impuesto NUMERIC(12,2) NOT NULL DEFAULT 0,
    total_neto NUMERIC(12,2) NOT NULL DEFAULT 0,
    forma_pago VARCHAR(20) NOT NULL,
    estado VARCHAR(20) NOT NULL DEFAULT 'FINALIZADA',
    usuario_id INTEGER NOT NULL,

    CONSTRAINT fk_venta_cliente
        FOREIGN KEY (cliente_id)
        REFERENCES cliente(id_cliente),

    CONSTRAINT fk_venta_usuario
        FOREIGN KEY (usuario_id)
        REFERENCES usuario(id_usuario),

    CONSTRAINT chk_venta_forma_pago
        CHECK (forma_pago IN ('CONTADO', 'CREDITO')),

    CONSTRAINT chk_venta_estado
        CHECK (
            estado IN (
                'EN_PROCESO',
                'FINALIZADA',
                'CANCELADA'
            )
        ),

    CONSTRAINT chk_venta_montos
        CHECK (
            total >= 0
            AND descuento >= 0
            AND impuesto >= 0
            AND total_neto >= 0
        )
);

CREATE TABLE detalle_venta (
    id_detalle_venta INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    venta_id INTEGER NOT NULL,
    producto_id INTEGER NOT NULL,
    cantidad NUMERIC(12,2) NOT NULL,
    precio_unitario NUMERIC(12,2) NOT NULL,
    descuento NUMERIC(12,2) NOT NULL DEFAULT 0,
    impuesto NUMERIC(12,2) NOT NULL DEFAULT 0,
    subtotal NUMERIC(12,2) NOT NULL,

    CONSTRAINT fk_detalle_venta_venta
        FOREIGN KEY (venta_id)
        REFERENCES venta(id_venta)
        ON DELETE CASCADE,

    CONSTRAINT fk_detalle_venta_producto
        FOREIGN KEY (producto_id)
        REFERENCES producto(id_producto),

    CONSTRAINT chk_detalle_cantidad
        CHECK (cantidad > 0),

    CONSTRAINT chk_detalle_precio
        CHECK (precio_unitario >= 0),

    CONSTRAINT chk_detalle_montos
        CHECK (
            descuento >= 0
            AND impuesto >= 0
            AND subtotal >= 0
        )
);

CREATE TABLE movimiento_inventario (
    id_movimiento INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    inventario_id INTEGER NOT NULL,
    tipo_movimiento VARCHAR(20) NOT NULL,
    cantidad NUMERIC(12,2) NOT NULL,
    fecha TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    referencia VARCHAR(100),
    observaciones TEXT,
    usuario_id INTEGER NOT NULL,

    CONSTRAINT fk_movimiento_inventario
        FOREIGN KEY (inventario_id)
        REFERENCES inventario(id_inventario),

    CONSTRAINT fk_movimiento_usuario
        FOREIGN KEY (usuario_id)
        REFERENCES usuario(id_usuario),

    CONSTRAINT chk_movimiento_tipo
        CHECK (
            tipo_movimiento IN (
                'ENTRADA',
                'SALIDA',
                'AJUSTE'
            )
        ),

    CONSTRAINT chk_movimiento_cantidad
        CHECK (cantidad > 0)
);

CREATE TABLE cuenta_por_cobrar (
    id_cxc INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    cliente_id INTEGER NOT NULL,
    venta_id INTEGER NOT NULL UNIQUE,
    fecha DATE NOT NULL DEFAULT CURRENT_DATE,
    fecha_vencimiento DATE NOT NULL,
    total NUMERIC(12,2) NOT NULL,
    saldo_pendiente NUMERIC(12,2) NOT NULL,
    estado VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE',

    CONSTRAINT fk_cxc_cliente
        FOREIGN KEY (cliente_id)
        REFERENCES cliente(id_cliente),

    CONSTRAINT fk_cxc_venta
        FOREIGN KEY (venta_id)
        REFERENCES venta(id_venta),

    CONSTRAINT chk_cxc_total
        CHECK (total >= 0),

    CONSTRAINT chk_cxc_saldo
        CHECK (
            saldo_pendiente >= 0
            AND saldo_pendiente <= total
        ),

    CONSTRAINT chk_cxc_estado
        CHECK (
            estado IN (
                'PENDIENTE',
                'PARCIAL',
                'PAGADA',
                'VENCIDA'
            )
        ),

    CONSTRAINT chk_cxc_fechas
        CHECK (fecha_vencimiento >= fecha)
);

CREATE TABLE pago (
    id_pago INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    cxc_id INTEGER NOT NULL,
    fecha_pago DATE NOT NULL DEFAULT CURRENT_DATE,
    monto NUMERIC(12,2) NOT NULL,
    metodo_pago VARCHAR(50) NOT NULL,
    referencia VARCHAR(100),
    observaciones TEXT,
    usuario_id INTEGER NOT NULL,

    CONSTRAINT fk_pago_cxc
        FOREIGN KEY (cxc_id)
        REFERENCES cuenta_por_cobrar(id_cxc),

    CONSTRAINT fk_pago_usuario
        FOREIGN KEY (usuario_id)
        REFERENCES usuario(id_usuario),

    CONSTRAINT chk_pago_monto
        CHECK (monto > 0)
);

CREATE TABLE respaldo (
    id_respaldo INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    fecha_hora TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    tipo VARCHAR(20) NOT NULL,
    ruta_archivo VARCHAR(500) NOT NULL,
    estado VARCHAR(20) NOT NULL,
    observaciones TEXT,
    usuario_id INTEGER,

    CONSTRAINT fk_respaldo_usuario
        FOREIGN KEY (usuario_id)
        REFERENCES usuario(id_usuario),

    CONSTRAINT chk_respaldo_tipo
        CHECK (
            tipo IN (
                'MANUAL',
                'PROGRAMADO'
            )
        ),

    CONSTRAINT chk_respaldo_estado
        CHECK (
            estado IN (
                'VALIDO',
                'FALLIDO',
                'INVALIDO'
            )
        )
);

-- ============================================================
-- ÍNDICES
-- ============================================================

CREATE INDEX idx_usuario_rol
    ON usuario(rol_id);

CREATE INDEX idx_producto_categoria
    ON producto(categoria);

CREATE INDEX idx_inventario_stock
    ON inventario(stock_actual, stock_minimo);

CREATE INDEX idx_venta_fecha
    ON venta(fecha);

CREATE INDEX idx_venta_cliente
    ON venta(cliente_id);

CREATE INDEX idx_venta_usuario
    ON venta(usuario_id);

CREATE INDEX idx_detalle_venta_venta
    ON detalle_venta(venta_id);

CREATE INDEX idx_detalle_venta_producto
    ON detalle_venta(producto_id);

CREATE INDEX idx_movimiento_inventario_fecha
    ON movimiento_inventario(fecha);

CREATE INDEX idx_movimiento_inventario_inventario
    ON movimiento_inventario(inventario_id);

CREATE INDEX idx_cxc_cliente
    ON cuenta_por_cobrar(cliente_id);

CREATE INDEX idx_cxc_vencimiento
    ON cuenta_por_cobrar(fecha_vencimiento);

CREATE INDEX idx_cxc_estado
    ON cuenta_por_cobrar(estado);

CREATE INDEX idx_pago_cxc
    ON pago(cxc_id);

CREATE INDEX idx_pago_fecha
    ON pago(fecha_pago);

CREATE INDEX idx_respaldo_fecha
    ON respaldo(fecha_hora);

-- ============================================================
-- DATOS INICIALES
-- ============================================================

INSERT INTO rol (nombre, descripcion)
VALUES
    (
        'ADMINISTRADOR',
        'Acceso a las funciones administrativas del sistema'
    ),
    (
        'VENDEDOR',
        'Acceso a las funciones operativas de ventas y clientes'
    ),
    (
        'GUEST',
        'Acceso limitado al sistema hasta que un administrador asigne un rol'
    );

-- Usuario administrador inicial
INSERT INTO usuario (
    nombre,
    usuario,
    contrasena_hash,
    email,
    rol_id
)
VALUES (
    'Administrador de Prueba',
    'admin',
    'AQAAAAIAAYagAAAAEJ3o8FnUj5LOBmvJOZt4uVWhC9DKdO3yogeAPQgnCXpVX/Fte6HtSnrxhGtcW/+G3g==',
    'admin@charlieshop.local',
    1
);

-- ============================================================
-- FIN DEL SCRIPT
-- ============================================================