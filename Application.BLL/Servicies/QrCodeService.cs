using Application.DAL.Models;
using Microsoft.EntityFrameworkCore;
using Application.DAL.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.BLL.Servicies
{
    public class QrCodeService
    {
        private readonly AppDbContext _context;
        private readonly LiveQrService _liveQrService;

        public QrCodeService(AppDbContext context, LiveQrService liveQrService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _liveQrService = liveQrService ?? throw new ArgumentNullException(nameof(liveQrService));
        }

        public async Task<CompanyQRCode?> GetAsync(int id)
        {
            return await _context.CompanyQRCodes
                .Include(q => q.Company)
                .FirstOrDefaultAsync(q => q.Id == id);
        }

        /// <summary>
        /// Create a new QR for the given company. Prior to creating, deactivate any currently active QR(s).
        /// This method is transactional to avoid races.
        /// </summary>
        public async Task<CompanyQRCode> CreateAsync(int companyId, TimeSpan validFor = default)
        {
            if (validFor == default)
                validFor = TimeSpan.FromDays(7);

            var company = await _context.Companies.FindAsync(companyId);
            if (company == null || !company.IsActive)
                throw new InvalidOperationException("Company not found or inactive.");

            // Use UTC consistently
            var now = DateTime.UtcNow;
            var expiry = now.Add(validFor);

            // Start a transaction so deactivation + creation are atomic
            await using (var tx = await _context.Database.BeginTransactionAsync())
            {
                // Deactivate any previously active QR(s) for this company
                var activeQrs = await _context.CompanyQRCodes
                    .Where(q => q.CompanyId == companyId && q.IsActive)
                    .ToListAsync();

                if (activeQrs.Count > 0)
                {
                    foreach (var a in activeQrs)
                        a.IsActive = false;

                    _context.CompanyQRCodes.UpdateRange(activeQrs);
                    await _context.SaveChangesAsync();
                }

                // create the new QR record (active = true)
                var qrRecord = new CompanyQRCode
                {
                    CompanyId = companyId,
                    QRCodeToken = Guid.NewGuid().ToString("N"),
                    GeneratedAt = now,
                    ExpiryDate = expiry,
                    IsActive = true
                };

                _context.CompanyQRCodes.Add(qrRecord);
                await _context.SaveChangesAsync();

                await tx.CommitAsync();
                return qrRecord;
            }
        }

        /// <summary>
        /// Returns an active, non-expired QR for the company.
        /// If none exists or the latest has expired, it deactivates previous active(s) and creates a new one.
        /// This method avoids creating multiple active tokens concurrently by using a transaction.
        /// </summary>
        public async Task<CompanyQRCode> RegenerateIfExpiredAsync(int companyId, TimeSpan validFor = default)
        {
            if (validFor == default)
                validFor = TimeSpan.FromDays(7);

            var company = await _context.Companies
                .Include(c => c.CompanyQRCodes)
                .FirstOrDefaultAsync(c => c.Id == companyId);

            if (company == null)
                throw new InvalidOperationException("Company not found.");

            if (!company.IsActive)
                throw new InvalidOperationException("Inactive companies cannot have QR codes.");

            var now = DateTime.UtcNow;

            // Find the most-recent active QR (if any)
            var activeLatest = await _context.CompanyQRCodes
                .Where(q => q.CompanyId == companyId && q.IsActive)
                .OrderByDescending(q => q.GeneratedAt)
                .FirstOrDefaultAsync();

            if (activeLatest != null && activeLatest.ExpiryDate > now)
            {
                // Active and not expired -> return it
                return activeLatest;
            }

            // Otherwise we need to create a new one. Use the CreateAsync path which deactivates any active rows
            return await CreateAsync(companyId, validFor);
        }
    }
}
