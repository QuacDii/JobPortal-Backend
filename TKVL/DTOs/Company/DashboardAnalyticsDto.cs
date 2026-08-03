using System;
using System.Collections.Generic;

namespace TKVL.DTOs.Company
{
    public class DashboardAnalyticsDto
    {
        public SummaryKpiDto Summary { get; set; } = new();
        public ChartsDataDto Charts { get; set; } = new();
        public List<TopJobItemDto> TopJobs { get; set; } = new();
    }

    public class SummaryKpiDto
    {
        public int TinDangDang { get; set; }
        public int HoSoMoiChuaDuyet { get; set; }
        public int LuotXemCvConLai { get; set; }
        public int TongLuotXemTin { get; set; }
        public int TongCvNop { get; set; }
        public double TyLeChuyenDoi { get; set; } // %
    }

    public class ChartsDataDto
    {
        public List<DailyTrendItemDto> DailyTrends { get; set; } = new();
        public List<StatusDistributionItemDto> StatusDistribution { get; set; } = new();
    }

    public class DailyTrendItemDto
    {
        public string Date { get; set; } = string.Empty; // DD/MM
        public int Views { get; set; }
        public int Applications { get; set; }
    }

    public class StatusDistributionItemDto
    {
        public string StatusName { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class TopJobItemDto
    {
        public int MaTin { get; set; }
        public string TieuDe { get; set; } = string.Empty;
        public int LuotXem { get; set; }
        public int SoCvNop { get; set; }
        public byte TrangThai { get; set; }
        public DateTime NgayDang { get; set; }
    }
}