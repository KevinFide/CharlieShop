
CREATE OR REPLACE PROCEDURE public.sp_crear_cliente(
    IN p_nombre VARCHAR(100),
    IN p_apellido VARCHAR(100),
    IN p_identificacion VARCHAR(30),
    IN p_telefono VARCHAR(20),
    IN p_email VARCHAR(100),
    IN p_direccion VARCHAR(200),
    INOUT p_resultado VARCHAR(30)
)
LANGUAGE plpgsql
AS $$
BEGIN
    IF p_identificacion IS NOT NULL
       AND EXISTS (
           SELECT 1
           FROM public.cliente
           WHERE identificacion = p_identificacion
       )
    THEN
        p_resultado := 'IDENTIFICACION_EXISTENTE';
        RETURN;
    END IF;

    IF p_telefono IS NOT NULL
       AND EXISTS (
           SELECT 1
           FROM public.cliente
           WHERE telefono = p_telefono
       )
    THEN
        p_resultado := 'TELEFONO_EXISTENTE';
        RETURN;
    END IF;

    INSERT INTO public.cliente (
        nombre,
        apellido,
        identificacion,
        telefono,
        email,
        direccion
    )
    VALUES (
        p_nombre,
        p_apellido,
        p_identificacion,
        p_telefono,
        p_email,
        p_direccion
    );

    p_resultado := 'CREADO';

EXCEPTION
    WHEN unique_violation THEN
        p_resultado := 'DATO_DUPLICADO';
END;
$$;


CREATE OR REPLACE PROCEDURE public.sp_obtener_clientes(
    INOUT p_clientes REFCURSOR
)
LANGUAGE plpgsql
AS $$
BEGIN
    OPEN p_clientes FOR
        SELECT
            id_cliente,
            nombre,
            apellido,
            identificacion,
            telefono,
            email,
            direccion,
            fecha_registro,
            estado
        FROM public.cliente
        ORDER BY id_cliente DESC;
END;
$$;
