using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace TKVL.Models;

public partial class JobPortalDbContext : DbContext
{
    public JobPortalDbContext()
    {
    }

    public JobPortalDbContext(DbContextOptions<JobPortalDbContext> options)
        : base(options)
    {
    }
    public virtual DbSet<MauCV> MauCVs { get; set; }

    public virtual DbSet<DanhMucMau> DanhMucMaus { get; set; }

    public virtual DbSet<PhanLoaiMau> PhanLoaiMaus { get; set; }

    public virtual DbSet<MauSac_MauCV> MauSacs { get; set; }

    public virtual DbSet<ChiTietViTri> ChiTietViTris { get; set; }

    public virtual DbSet<LichSuMoKhoaCV> LichSuMoKhoaCvs { get; set; }

    public virtual DbSet<JobAlert> JobAlerts { get; set; }

    public virtual DbSet<CongTy> CongTies { get; set; }

    public virtual DbSet<Cv> Cvs { get; set; }

    public virtual DbSet<DonUngTuyen> DonUngTuyens { get; set; }

    public virtual DbSet<GiaoDich> GiaoDiches { get; set; }

    public virtual DbSet<GoiDichVu> GoiDichVus { get; set; }

    public virtual DbSet<KyNang> KyNangs { get; set; }

    public virtual DbSet<NganhNghe> NganhNghes { get; set; }

    public virtual DbSet<PhuongXa> PhuongXas { get; set; }

    public virtual DbSet<ThanhPho> ThanhPhos { get; set; }

    public virtual DbSet<CV_CauTrucMuc> CV_CauTrucMucs { get; set; }

    public virtual DbSet<TinDaLuu> TinDaLuus { get; set; }

    public virtual DbSet<TinTuyenDung> TinTuyenDungs { get; set; }

    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<ChiTietPhanTichAi> ChiTietPhanTichAis { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChiTietViTri>(entity =>
        {
            entity.HasKey(e => e.MaViTri).HasName("PK__ChiTietV__1D9EB9AFB5B463F0");

            entity.ToTable("ChiTietViTri");

            entity.Property(e => e.MaViTri).HasColumnName("maViTri");
            entity.Property(e => e.Luong)
                .HasMaxLength(50)
                .HasColumnName("luong");
            entity.Property(e => e.MaNganh).HasColumnName("maNganh");
            entity.Property(e => e.MaPhuong).HasColumnName("maPhuong");
            entity.Property(e => e.MaTin).HasColumnName("maTin");
            entity.Property(e => e.MoTaCongViec).HasColumnName("moTaCongViec");
            entity.Property(e => e.QuyenLoi).HasColumnName("quyenLoi");
            entity.Property(e => e.SoLuongTuyen)
                .HasDefaultValue(1)
                .HasColumnName("soLuongTuyen");
            entity.Property(e => e.TenViTri)
                .HasMaxLength(150)
                .HasColumnName("tenViTri");
            entity.Property(e => e.YeuCauUngVien).HasColumnName("yeuCauUngVien");

            entity.HasOne(d => d.MaNganhNavigation).WithMany(p => p.ChiTietViTris)
                .HasForeignKey(d => d.MaNganh)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ChiTietViTri_NganhNghe");

            entity.HasOne(d => d.MaPhuongNavigation).WithMany(p => p.ChiTietViTris)
                .HasForeignKey(d => d.MaPhuong)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ChiTietViTri_PhuongXa");

            entity.HasOne(d => d.MaTinNavigation).WithMany(p => p.ChiTietViTris)
                .HasForeignKey(d => d.MaTin)
                .HasConstraintName("FK_ChiTietViTri_TinTuyenDung");

            entity.HasMany(d => d.MaKyNangs).WithMany(p => p.MaViTris)
                .UsingEntity<Dictionary<string, object>>(
                    "ViTriKyNang",
                    r => r.HasOne<KyNang>().WithMany()
                        .HasForeignKey("MaKyNang")
                        .HasConstraintName("FK_ViTriKyNang_KyNang"),
                    l => l.HasOne<ChiTietViTri>().WithMany()
                        .HasForeignKey("MaViTri")
                        .HasConstraintName("FK_ViTriKyNang_ChiTietViTri"),
                    j =>
                    {
                        j.HasKey("MaViTri", "MaKyNang").HasName("PK__ViTri_Ky__F7C5048EA33CCCA9");
                        j.ToTable("ViTri_KyNang");
                        j.IndexerProperty<int>("MaViTri").HasColumnName("maViTri");
                        j.IndexerProperty<int>("MaKyNang").HasColumnName("maKyNang");
                    });

            entity.Property(e => e.NganhNgheKhac)
              .HasMaxLength(150)
              .HasColumnName("nganhNgheKhac");
        });

        modelBuilder.Entity<CongTy>(entity =>
        {
            entity.HasKey(e => e.MaCongTy).HasName("PK__CongTy__EAECFE7B76A7CB70");

            entity.ToTable("CongTy");

            entity.HasIndex(e => e.MaUser, "UQ__CongTy__18B21FF0E38144C8").IsUnique();

            entity.HasIndex(e => e.MaSoThue, "UQ__CongTy__6A0F0170C6948BB6").IsUnique();

            entity.Property(e => e.MaCongTy).HasColumnName("maCongTy");
            entity.Property(e => e.DiaChi)
                .HasMaxLength(255)
                .HasColumnName("diaChi");
            entity.Property(e => e.Logo)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("logo");
            entity.Property(e => e.MaSoThue)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("maSoThue");
            entity.Property(e => e.MaUser).HasColumnName("maUser");
            entity.Property(e => e.MoTa).HasColumnName("moTa");
            entity.Property(e => e.QuyMo)
                .HasMaxLength(100)
                .HasColumnName("quyMo");
            entity.Property(e => e.TenCongTy)
                .HasMaxLength(255)
                .HasColumnName("tenCongTy");
            entity.Property(e => e.TrangThai).HasColumnName("trangThai");

            entity.HasOne(d => d.MaUserNavigation).WithOne(p => p.CongTy)
                .HasForeignKey<CongTy>(d => d.MaUser)
                .HasConstraintName("FK_CongTy_User");
        });

        modelBuilder.Entity<Cv>(entity =>
        {
            entity.HasKey(e => e.MaCv).HasName("PK__CV__7A3E0CF073B2E13F");

            entity.ToTable("CV");

            entity.Property(e => e.MaCv).HasColumnName("maCV");
            entity.Property(e => e.DuLieuCv).HasColumnName("duLieuCV");
            entity.Property(e => e.DuongDan)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("duongDan");
            entity.Property(e => e.IsPublic).HasColumnName("isPublic");
            entity.Property(e => e.MaUser).HasColumnName("maUser");
            entity.Property(e => e.TieuDe)
                .HasMaxLength(100)
                .HasColumnName("tieuDe");

            entity.HasOne(d => d.MaUserNavigation).WithMany(p => p.Cvs)
                .HasForeignKey(d => d.MaUser)
                .HasConstraintName("FK_CV_User");
        });

        modelBuilder.Entity<DonUngTuyen>(entity =>
        {
            entity.HasKey(e => e.MaDon).HasName("PK__DonUngTu__2431086DFCC52540");

            entity.ToTable("DonUngTuyen");

            entity.Property(e => e.MaDon).HasColumnName("maDon");
            entity.Property(e => e.GhiChu)
                .HasMaxLength(255)
                .HasColumnName("ghiChu");
            entity.Property(e => e.MaCv).HasColumnName("maCV");
            entity.Property(e => e.MaViTri).HasColumnName("maViTri");
            entity.Property(e => e.NgayNop)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("ngayNop");
            entity.Property(e => e.ThuGioiThieu).HasColumnName("thuGioiThieu");
            entity.Property(e => e.TrangThai).HasColumnName("trangThai");

            entity.HasOne(d => d.MaCvNavigation).WithMany(p => p.DonUngTuyens)
                .HasForeignKey(d => d.MaCv)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DonUngTuyen_CV");

            entity.HasOne(d => d.MaViTriNavigation).WithMany(p => p.DonUngTuyens)
                .HasForeignKey(d => d.MaViTri)
                .HasConstraintName("FK_DonUngTuyen_ChiTietViTri");
        });

        modelBuilder.Entity<GiaoDich>(entity =>
        {
            entity.HasKey(e => e.MaGd).HasName("PK__GiaoDich__7A3E2D67EF992E32");

            entity.ToTable("GiaoDich");

            entity.Property(e => e.MaGd).HasColumnName("maGD");
            entity.Property(e => e.LoaiGiaoDich).HasColumnName("loaiGiaoDich");
            entity.Property(e => e.MaGiaoDichDoiTac)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("maGiaoDichDoiTac");
            entity.Property(e => e.MaGoi).HasColumnName("maGoi");
            entity.Property(e => e.MaUser).HasColumnName("maUser");
            entity.Property(e => e.NgayGd)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("ngayGD");
            entity.Property(e => e.PhuongThuc)
                .HasMaxLength(50)
                .HasColumnName("phuongThuc");
            entity.Property(e => e.SoTien)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("soTien");
            entity.Property(e => e.TrangThai).HasColumnName("trangThai");

            entity.HasOne(d => d.MaGoiNavigation).WithMany(p => p.GiaoDiches)
                .HasForeignKey(d => d.MaGoi)
                .HasConstraintName("FK_GiaoDich_GoiDichVu");

            entity.HasOne(d => d.MaUserNavigation).WithMany(p => p.GiaoDiches)
                .HasForeignKey(d => d.MaUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GiaoDich_User");
        });

        modelBuilder.Entity<GoiDichVu>(entity =>
        {
            entity.HasKey(e => e.MaGoi).HasName("PK__GoiDichV__2D87A9A0DF458666");

            entity.ToTable("GoiDichVu");

            entity.Property(e => e.MaGoi).HasColumnName("maGoi");
            entity.Property(e => e.GiaTien)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("giaTien");
            entity.Property(e => e.LoaiGoi).HasColumnName("loaiGoi");
            entity.Property(e => e.SoLuotXemCv).HasColumnName("soLuotXemCV");
            entity.Property(e => e.DonViThoiGian).HasColumnName("donViThoiGian");
            entity.Property(e => e.TenGoi)
                .HasMaxLength(100)
                .HasColumnName("tenGoi");
            entity.Property(e => e.GiaKhuyenMai)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("giaKhuyenMai");
        });

        modelBuilder.Entity<KyNang>(entity =>
        {
            entity.HasKey(e => e.MaKyNang).HasName("PK__KyNang__A5BBD21FE9BCB984");

            entity.ToTable("KyNang");

            entity.Property(e => e.MaKyNang).HasColumnName("maKyNang");
            entity.Property(e => e.TenKyNang)
                .HasMaxLength(100)
                .HasColumnName("tenKyNang");
            entity.Property(e => e.TrangThai)
                .HasDefaultValue(true)
                .HasColumnName("trangThai");
        });

        modelBuilder.Entity<NganhNghe>(entity =>
        {
            entity.HasKey(e => e.MaNganh).HasName("PK__NganhNgh__4E0C021750B7BE5B");

            entity.ToTable("NganhNghe");

            entity.Property(e => e.MaNganh).HasColumnName("maNganh");
            entity.Property(e => e.TenNganh)
                .HasMaxLength(100)
                .HasColumnName("tenNganh");
            entity.Property(e => e.TrangThai)
                .HasDefaultValue(true)
                .HasColumnName("trangThai");
        });

        modelBuilder.Entity<PhuongXa>(entity =>
        {
            entity.HasKey(e => e.MaPhuong).HasName("PK__PhuongXa__DF98DF6714DE7A6F");

            entity.ToTable("PhuongXa");

            entity.Property(e => e.MaPhuong).HasColumnName("maPhuong");
            entity.Property(e => e.MaTp).HasColumnName("maTP");
            entity.Property(e => e.TenPhuong)
                .HasMaxLength(100)
                .HasColumnName("tenPhuong");

            entity.HasOne(d => d.MaTpNavigation).WithMany(p => p.PhuongXas)
                .HasForeignKey(d => d.MaTp)
                .HasConstraintName("FK_PhuongXa_ThanhPho");
        });

        modelBuilder.Entity<ThanhPho>(entity =>
        {
            entity.HasKey(e => e.MaTp).HasName("PK__ThanhPho__7A22625BAD627896");

            entity.ToTable("ThanhPho");

            entity.Property(e => e.MaTp).HasColumnName("maTP");
            entity.Property(e => e.TenTp)
                .HasMaxLength(100)
                .HasColumnName("tenTP");
        });

        modelBuilder.Entity<TinDaLuu>(entity =>
        {
            entity.HasKey(e => new { e.MaUser, e.MaViTri }).HasName("PK__TinDaLuu__F96BF46BF9E6D8A3");

            entity.ToTable("TinDaLuu");

            entity.Property(e => e.MaUser).HasColumnName("maUser");
            entity.Property(e => e.MaViTri).HasColumnName("maViTri");
            entity.Property(e => e.NgayLuu)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("ngayLuu");

            entity.HasOne(d => d.MaUserNavigation).WithMany(p => p.TinDaLuus)
                .HasForeignKey(d => d.MaUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TinDaLuu_User");

            entity.HasOne(d => d.MaViTriNavigation).WithMany(p => p.TinDaLuus)
                .HasForeignKey(d => d.MaViTri)
                .HasConstraintName("FK_TinDaLuu_ChiTietViTri");
        });

        modelBuilder.Entity<TinTuyenDung>(entity =>
        {
            entity.HasKey(e => e.MaTin).HasName("PK__TinTuyen__0FC4052A75963FBB");

            entity.ToTable("TinTuyenDung");

            entity.Property(e => e.MaTin).HasColumnName("maTin");
            entity.Property(e => e.IsPromoted).HasColumnName("isPromoted");
            entity.Property(e => e.MaCongTy).HasColumnName("maCongTy");
            entity.Property(e => e.NgayHetHan)
                .HasColumnType("datetime")
                .HasColumnName("ngayHetHan");
            entity.Property(e => e.TieuDeChienDich)
                .HasMaxLength(200)
                .HasColumnName("tieuDeChienDich");
            entity.Property(e => e.TrangThai).HasColumnName("trangThai");

            entity.HasOne(d => d.MaCongTyNavigation).WithMany(p => p.TinTuyenDungs)
                .HasForeignKey(d => d.MaCongTy)
                .HasConstraintName("FK_TinTuyenDung_CongTy");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.MaUser).HasName("PK__User__18B21FF1C0EDDC7B");

            entity.ToTable("User");

            entity.HasIndex(e => e.GoogleId, "UQ__User__0DA2E4829AC45A41").IsUnique();

            entity.HasIndex(e => e.FacebookId, "UQ__User__1DCE2AA87028DFD3").IsUnique();

            entity.HasIndex(e => e.Email, "UQ__User__AB6E61649BE61696").IsUnique();

            entity.Property(e => e.MaUser).HasColumnName("maUser");
            entity.Property(e => e.Avatar)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("avatar");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("email");
            entity.Property(e => e.FacebookId)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("facebookId");
            entity.Property(e => e.GoogleId)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("googleId");
            entity.Property(e => e.HoTen)
                .HasMaxLength(100)
                .HasColumnName("hoTen");
            entity.Property(e => e.MatKhau)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("matKhau");
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("ngayTao");
            entity.Property(e => e.SoDuVi)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("soDuVi");
            entity.Property(e => e.TrangThai)
                .HasDefaultValue(true)
                .HasColumnName("trangThai");
            entity.Property(e => e.VaiTro)
                .HasDefaultValue((byte)2)
                .HasColumnName("vaiTro");
        });

        OnModelCreatingPartial(modelBuilder);
        modelBuilder.Entity<LichSuMoKhoaCV>(entity =>
        {
            entity.HasKey(e => e.MaLichSu);

            entity.HasOne(d => d.MaUserNavigation)
                  .WithMany()
                  .HasForeignKey(d => d.MaUser)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.MaCvNavigation)
                  .WithMany()
                  .HasForeignKey(d => d.MaCv)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobAlert>(entity =>
        {
            entity.HasKey(e => e.MaAlert);

            entity.HasOne(d => d.MaUserNavigation)
                  .WithMany()
                  .HasForeignKey(d => d.MaUser)
                  .OnDelete(DeleteBehavior.Restrict); 
        });

        modelBuilder.Entity<PhanLoaiMau>(entity =>
        {
            entity.HasKey(e => new { e.MaMau, e.MaDanhMuc }); 

            entity.HasOne(d => d.MauCVNavigation)
                .WithMany(p => p.PhanLoaiMaus)
                .HasForeignKey(d => d.MaMau)
                .HasConstraintName("FK_PhanLoaiMau_MauCV")
                .OnDelete(DeleteBehavior.Cascade); 

            entity.HasOne(d => d.DanhMucMauNavigation)
                .WithMany(p => p.PhanLoaiMaus)
                .HasForeignKey(d => d.MaDanhMuc)
                .HasConstraintName("FK_PhanLoaiMau_DanhMuc")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MauSac_MauCV>(entity =>
        {
            entity.HasOne(d => d.MauCVNavigation)
                .WithMany(p => p.MauSacs)
                .HasForeignKey(d => d.MaMau)
                .HasConstraintName("FK_MauSac_MauCV")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Cv>(entity =>
        {
            entity.HasOne(d => d.MaMauNavigation)
                .WithMany(p => p.Cvs)
                .HasForeignKey(d => d.MaMau)
                .HasConstraintName("FK_Cv_MauCV")
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ChiTietPhanTichAi>(entity =>
        {
            entity.HasKey(e => e.MaPhanTich);
            entity.ToTable("ChiTietPhanTichAi");

            entity.Property(e => e.MaPhanTich).HasColumnName("maPhanTich");
            entity.Property(e => e.MaDon).HasColumnName("maDon");

            entity.Property(e => e.DiemMatchingTong).HasColumnName("diemMatchingTong");
            entity.Property(e => e.DiemKyNang).HasColumnName("diemKyNang");
            entity.Property(e => e.DiemKinhNghiem).HasColumnName("diemKinhNghiem");
            entity.Property(e => e.DiemLinhVuc).HasColumnName("diemLinhVuc");
            entity.Property(e => e.DiemCapBac).HasColumnName("diemCapBac");

            entity.Property(e => e.DiemManhTieuBieu).HasColumnName("diemManhTieuBieu");
            entity.Property(e => e.DiemConThieu).HasColumnName("diemConThieu");
            entity.Property(e => e.ThongTinHoSoTrichXuatJson).HasColumnName("thongTinHoSoTrichXuatJson");

            // RÀNG BUỘC QUAN HỆ 1 - 1: DonUngTuyen là bảng chính, ChiTietPhanTichAi giữ khóa ngoại maDon
            entity.HasOne(d => d.MaDonNavigation)
                  .WithOne(p => p.ChiTietPhanTichAi)
                  .HasForeignKey<ChiTietPhanTichAi>(d => d.MaDon)
                  .OnDelete(DeleteBehavior.Cascade) // Khi nhà tuyển dụng xóa đơn ứng tuyển, bản phân tích AI tự động bay màu theo
                  .HasConstraintName("FK_ChiTietPhanTichAi_DonUngTuyen");
        });
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
