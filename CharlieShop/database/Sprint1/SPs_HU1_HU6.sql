-- ============================================================
-- CHARLIE SHOP - STORED PROCEDURES
-- AccountController
-- PostgreSQL
-- ============================================================

DROP PROCEDURE IF EXISTS sp_obtener_usuario_por_email(VARCHAR);
DROP PROCEDURE IF EXISTS sp_actualizar_contrasena(INTEGER, VARCHAR);
DROP PROCEDURE IF EXISTS sp_obtener_usuario_login(VARCHAR);

-- ============================================================
-- 1. Obtener usuario activo por correo
-- ============================================================

CREATE OR REPLACE PROCEDURE sp_obtener_usuario_por_email(
    IN p_email VARCHAR,
    OUT o_id_usuario INTEGER,
    OUT o_nombre VARCHAR,
    OUT o_usuario VARCHAR,
    OUT o_contrasena_hash VARCHAR,
    OUT o_email VARCHAR,
    OUT o_rol_id INTEGER,
    OUT o_estado BOOLEAN,
    OUT o_fecha_creacion TIMESTAMP
)
LANGUAGE plpgsql
AS $$
BEGIN
    o_id_usuario := NULL;
    o_nombre := NULL;
    o_usuario := NULL;
    o_contrasena_hash := NULL;
    o_email := NULL;
    o_rol_id := NULL;
    o_estado := NULL;
    o_fecha_creacion := NULL;

    SELECT
        u.id_usuario,
        u.nombre,
        u.usuario,
        u.contrasena_hash,
        u.email,
        u.rol_id,
        u.estado,
        u.fecha_creacion
    INTO
        o_id_usuario,
        o_nombre,
        o_usuario,
        o_contrasena_hash,
        o_email,
        o_rol_id,
        o_estado,
        o_fecha_creacion
    FROM usuario u
    WHERE LOWER(u.email) = LOWER(p_email)
      AND u.estado = TRUE
    LIMIT 1;
END;
$$;

-- ============================================================
-- 2. Actualizar contraseña
-- ============================================================

CREATE OR REPLACE PROCEDURE sp_actualizar_contrasena(
    IN p_id_usuario INTEGER,
    IN p_contrasena_hash VARCHAR
)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE usuario
    SET contrasena_hash = p_contrasena_hash
    WHERE id_usuario = p_id_usuario;
END;
$$;

-- ============================================================
-- 3. Obtener usuario para Login
-- ============================================================

CREATE OR REPLACE PROCEDURE sp_obtener_usuario_login(
    IN p_usuario VARCHAR,
    OUT o_id_usuario INTEGER,
    OUT o_nombre VARCHAR,
    OUT o_usuario_nombre VARCHAR,
    OUT o_contrasena_hash VARCHAR,
    OUT o_email VARCHAR,
    OUT o_rol_id INTEGER,
    OUT o_estado BOOLEAN,
    OUT o_fecha_creacion TIMESTAMP,
    OUT o_nombre_rol VARCHAR
)
LANGUAGE plpgsql
AS $$
BEGIN
    o_id_usuario := NULL;
    o_nombre := NULL;
    o_usuario_nombre := NULL;
    o_contrasena_hash := NULL;
    o_email := NULL;
    o_rol_id := NULL;
    o_estado := NULL;
    o_fecha_creacion := NULL;
    o_nombre_rol := NULL;

    SELECT
        u.id_usuario,
        u.nombre,
        u.usuario,
        u.contrasena_hash,
        u.email,
        u.rol_id,
        u.estado,
        u.fecha_creacion,
        r.nombre
    INTO
        o_id_usuario,
        o_nombre,
        o_usuario_nombre,
        o_contrasena_hash,
        o_email,
        o_rol_id,
        o_estado,
        o_fecha_creacion,
        o_nombre_rol
    FROM usuario u
    INNER JOIN rol r
        ON r.id_rol = u.rol_id
    WHERE u.usuario = p_usuario
    LIMIT 1;
END;
$$;

---------------------------------------------------------

-- ============================================================
-- CHARLIE SHOP - STORED PROCEDURES
-- UsuariosController
-- PostgreSQL
-- ============================================================

DROP PROCEDURE IF EXISTS sp_verificar_usuario_existente(VARCHAR);
DROP PROCEDURE IF EXISTS sp_verificar_email_existente(VARCHAR);
DROP PROCEDURE IF EXISTS sp_obtener_rol_guest();
DROP PROCEDURE IF EXISTS sp_crear_usuario(VARCHAR, VARCHAR, VARCHAR, VARCHAR, INTEGER, BOOLEAN);
DROP PROCEDURE IF EXISTS sp_obtener_usuario_contrasena(VARCHAR);
DROP PROCEDURE IF EXISTS sp_actualizar_contrasena_usuario(INTEGER, VARCHAR);

-- ============================================================
-- 1. Verificar si el nombre de usuario ya existe
-- ============================================================

CREATE OR REPLACE PROCEDURE sp_verificar_usuario_existente(
    IN p_usuario VARCHAR,
    OUT o_existe BOOLEAN
)
LANGUAGE plpgsql
AS $$
BEGIN
    SELECT EXISTS (
        SELECT 1
        FROM usuario
        WHERE usuario = p_usuario
    )
    INTO o_existe;
END;
$$;

-- ============================================================
-- 2. Verificar si el correo ya existe
-- ============================================================

CREATE OR REPLACE PROCEDURE sp_verificar_email_existente(
    IN p_email VARCHAR,
    OUT o_existe BOOLEAN
)
LANGUAGE plpgsql
AS $$
BEGIN
    SELECT EXISTS (
        SELECT 1
        FROM usuario
        WHERE LOWER(email) = LOWER(p_email)
    )
    INTO o_existe;
END;
$$;

-- ============================================================
-- 3. Obtener el rol GUEST activo
-- ============================================================

CREATE OR REPLACE PROCEDURE sp_obtener_rol_guest(
    OUT o_id_rol INTEGER
)
LANGUAGE plpgsql
AS $$
BEGIN
    SELECT id_rol
    INTO o_id_rol
    FROM rol
    WHERE nombre = 'GUEST'
      AND estado = TRUE
    LIMIT 1;
END;
$$;

-- ============================================================
-- 4. Crear usuario
-- ============================================================

CREATE OR REPLACE PROCEDURE sp_crear_usuario(
    IN p_nombre VARCHAR,
    IN p_usuario VARCHAR,
    IN p_contrasena_hash VARCHAR,
    IN p_email VARCHAR,
    IN p_rol_id INTEGER,
    IN p_estado BOOLEAN
)
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO usuario (
        nombre,
        usuario,
        contrasena_hash,
        email,
        rol_id,
        estado
    )
    VALUES (
        p_nombre,
        p_usuario,
        p_contrasena_hash,
        p_email,
        p_rol_id,
        p_estado
    );
END;
$$;

-- ============================================================
-- 5. Obtener usuario autenticado para cambio de contraseña
-- ============================================================

CREATE OR REPLACE PROCEDURE sp_obtener_usuario_contrasena(
    IN p_usuario VARCHAR,
    OUT o_id_usuario INTEGER,
    OUT o_nombre VARCHAR,
    OUT o_usuario_nombre VARCHAR,
    OUT o_contrasena_hash VARCHAR,
    OUT o_email VARCHAR,
    OUT o_rol_id INTEGER,
    OUT o_estado BOOLEAN,
    OUT o_fecha_creacion TIMESTAMP
)
LANGUAGE plpgsql
AS $$
BEGIN
    o_id_usuario := NULL;
    o_nombre := NULL;
    o_usuario_nombre := NULL;
    o_contrasena_hash := NULL;
    o_email := NULL;
    o_rol_id := NULL;
    o_estado := NULL;
    o_fecha_creacion := NULL;

    SELECT
        u.id_usuario,
        u.nombre,
        u.usuario,
        u.contrasena_hash,
        u.email,
        u.rol_id,
        u.estado,
        u.fecha_creacion
    INTO
        o_id_usuario,
        o_nombre,
        o_usuario_nombre,
        o_contrasena_hash,
        o_email,
        o_rol_id,
        o_estado,
        o_fecha_creacion
    FROM usuario u
    WHERE u.usuario = p_usuario
      AND u.estado = TRUE
    LIMIT 1;
END;
$$;

-- ============================================================
-- 6. Actualizar contraseña del usuario
-- ============================================================

CREATE OR REPLACE PROCEDURE sp_actualizar_contrasena_usuario(
    IN p_id_usuario INTEGER,
    IN p_contrasena_hash VARCHAR
)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE usuario
    SET contrasena_hash = p_contrasena_hash
    WHERE id_usuario = p_id_usuario;
END;
$$;


-- ============================================================
-- 7. Listar usuarios
-- ============================================================


CREATE OR REPLACE PROCEDURE sp_obtener_usuarios(
    OUT o_usuarios JSON
)
LANGUAGE plpgsql
AS $$
BEGIN

    SELECT COALESCE(
        json_agg(
            json_build_object(
                'IdUsuario', u.id_usuario,
                'Nombre', u.nombre,
                'UsuarioNombre', u.usuario,
                'Email', u.email,
                'RolId', u.rol_id,
                'NombreRol', r.nombre,
                'Estado', u.estado,
                'FechaCreacion', u.fecha_creacion
            )
            ORDER BY u.nombre
        ),
        '[]'::json
    )
    INTO o_usuarios

    FROM usuario u

    INNER JOIN rol r
        ON r.id_rol = u.rol_id;

END;
$$;

-- ============================================================
-- 8. Listar usuarios editar
-- ============================================================

CREATE OR REPLACE PROCEDURE sp_obtener_usuario_editar(
    IN p_id_usuario INTEGER,
    OUT o_usuario JSON
)
LANGUAGE plpgsql
AS $$
BEGIN

    SELECT json_build_object(
        'IdUsuario', u.id_usuario,
        'Nombre', u.nombre,
        'Usuario', u.usuario,
        'Email', u.email,
        'RolId', u.rol_id,
        'Estado', u.estado
    )
    INTO o_usuario

    FROM usuario u

    WHERE u.id_usuario = p_id_usuario;

END;
$$;

-- ============================================================
-- 9. Obtener roles activos
-- ============================================================

CREATE OR REPLACE PROCEDURE sp_obtener_roles_activos(
    OUT o_roles JSON
)
LANGUAGE plpgsql
AS $$
BEGIN

    SELECT COALESCE(
        json_agg(
            json_build_object(
                'IdRol', r.id_rol,
                'Nombre', r.nombre
            )
            ORDER BY r.nombre
        ),
        '[]'::json
    )
    INTO o_roles

    FROM rol r

    WHERE r.estado = TRUE;

END;
$$;

-- ============================================================
-- 10. Actualizar Usuario
-- ============================================================


CREATE OR REPLACE PROCEDURE sp_actualizar_usuario(
    IN p_id_usuario INTEGER,
    IN p_nombre VARCHAR(100),
    IN p_usuario VARCHAR(50),
    IN p_email VARCHAR(100),
    IN p_rol_id INTEGER,
    IN p_estado BOOLEAN
)
LANGUAGE plpgsql
AS $$
BEGIN

    UPDATE usuario

    SET
        nombre = p_nombre,
        usuario = p_usuario,
        email = p_email,
        rol_id = p_rol_id,
        estado = p_estado

    WHERE id_usuario = p_id_usuario;

END;
$$;

-- ============================================================
-- 11. Verificar usuario
-- ============================================================

CREATE OR REPLACE PROCEDURE sp_verificar_usuario_editar(
    IN p_id_usuario INTEGER,
    IN p_usuario VARCHAR(50),
    OUT o_existe BOOLEAN
)
LANGUAGE plpgsql
AS $$
BEGIN

    SELECT EXISTS (
        SELECT 1
        FROM usuario
        WHERE usuario = p_usuario
          AND id_usuario <> p_id_usuario
    )
    INTO o_existe;

END;
$$;

-- ============================================================
-- 12. Verificar email
-- ============================================================

CREATE OR REPLACE PROCEDURE sp_verificar_email_editar(
    IN p_id_usuario INTEGER,
    IN p_email VARCHAR(100),
    OUT o_existe BOOLEAN
)
LANGUAGE plpgsql
AS $$
BEGIN

    SELECT EXISTS (
        SELECT 1
        FROM usuario
        WHERE email = p_email
          AND id_usuario <> p_id_usuario
    )
    INTO o_existe;

END;
$$;

-- ============================================================
-- 13. Cambiar estado usuario
-- ============================================================

CREATE OR REPLACE PROCEDURE sp_cambiar_estado_usuario(
    IN p_id_usuario INTEGER,
    IN p_estado BOOLEAN
)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE usuario
    SET estado = p_estado
    WHERE id_usuario = p_id_usuario;
END;
$$;