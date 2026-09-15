-- Run against each environment before user testing. The result must be empty.
-- Review mismatched accounts manually; do not infer their intended role from a shared firm record.
WITH BeklenenRoller AS
(
    SELECT u.Id,
           CASE
               WHEN u.KullaniciTipi = 1 THEN N'YetkiliServis'
               WHEN u.KullaniciTipi = 2 THEN N'Personel'
               WHEN u.KullaniciTipi = 3 AND u.SirketId IS NOT NULL THEN N'SirketAdmin'
               WHEN u.KullaniciTipi IN (3, 4) THEN N'GenelSistemAdmin'
               WHEN u.KullaniciTipi = 5 THEN N'SertifikaliFirma'
           END AS BeklenenRol,
           CASE WHEN u.KullaniciTipi IN (3, 4) AND u.SirketId IS NULL THEN 1 ELSE 0 END AS EskiSuperAdminIzni
    FROM dbo.Ys_AspNetUsers AS u
    WHERE u.KullaniciTipi BETWEEN 1 AND 5
),
YonetilenRoller AS
(
    SELECT N'YetkiliServis' AS Rol
    UNION ALL SELECT N'Personel'
    UNION ALL SELECT N'SirketAdmin'
    UNION ALL SELECT N'GenelSistemAdmin'
    UNION ALL SELECT N'SertifikaliFirma'
)
SELECT u.Id, u.Email, u.KullaniciTipi, b.BeklenenRol, r.Name AS AtananRol
FROM dbo.Ys_AspNetUsers AS u
JOIN BeklenenRoller AS b ON b.Id = u.Id
JOIN dbo.Ys_AspNetUserRoles AS ur ON ur.UserId = u.Id
JOIN dbo.Ys_AspNetRoles AS r ON r.Id = ur.RoleId
JOIN YonetilenRoller AS y ON y.Rol = r.Name
WHERE r.Name <> b.BeklenenRol
  AND NOT (b.EskiSuperAdminIzni = 1 AND r.Name = N'SuperAdmin')
UNION ALL
SELECT u.Id, u.Email, u.KullaniciTipi, b.BeklenenRol, NULL AS AtananRol
FROM dbo.Ys_AspNetUsers AS u
JOIN BeklenenRoller AS b ON b.Id = u.Id
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.Ys_AspNetUserRoles AS ur
    JOIN dbo.Ys_AspNetRoles AS r ON r.Id = ur.RoleId
    WHERE ur.UserId = u.Id
      AND r.Name = b.BeklenenRol
);
