-- Run against each environment before user testing. The result must be empty.
-- Review mismatched accounts manually; do not infer their intended role from a shared firm record.
SELECT u.Id, u.Email, u.KullaniciTipi, r.Name AS AtananRol
FROM dbo.Ys_AspNetUsers AS u
JOIN dbo.Ys_AspNetUserRoles AS ur ON ur.UserId = u.Id
JOIN dbo.Ys_AspNetRoles AS r ON r.Id = ur.RoleId
WHERE (r.Name = N'SertifikaliFirma' AND u.KullaniciTipi <> 5)
   OR (r.Name = N'YetkiliServis' AND u.KullaniciTipi <> 1);
