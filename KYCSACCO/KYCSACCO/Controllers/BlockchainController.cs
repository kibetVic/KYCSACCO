using KYCSACCO.Data;
using KYCSACCO.Models;
using KYCSACCO.Models.ViewModels;
using KYCSACCO.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KYCSACCO.Data;
using KYCSACCO.Models;
using KYCSACCO.Models.ViewModels;
using KYCSACCO.Services;

namespace KYCSACCO.Controllers
{
    [Authorize]
    public class BlockchainController : Controller
    {
        private readonly IBlockchainService _blockchainService;
        private readonly ILogger<BlockchainController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly ICompanyContextService _companyContext;

        public BlockchainController(
            IBlockchainService blockchainService,
            ILogger<BlockchainController> logger,
            ApplicationDbContext context,
            ICompanyContextService companyContext)
        {
            _blockchainService = blockchainService;
            _logger = logger;
            _context = context;
            _companyContext = companyContext;
        }

        // GET: /Blockchain/VerifyBlock/{blockHash}
        [HttpGet("Blockchain/VerifyBlock/{blockHash}")]
        public async Task<IActionResult> VerifyBlock(string blockHash)
        {
            try
            {
                var block = await _blockchainService.GetBlockAsync(blockHash);

                if (block == null)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Block not found"
                    });
                }

                // Validate this specific block
                var isValid = await ValidateBlock(block);

                // Also validate the entire chain
                var isChainValid = await _blockchainService.VerifyBlockchainAsync();

                if (isValid && isChainValid)
                {
                    return Ok(new
                    {
                        success = true,
                        message = "Block is valid",
                        block = block.BlockHash,
                        blockId = block.BlockId,
                        confirmed = block.Confirmed,
                        previousHash = block.PreviousHash,
                        merkleRoot = block.MerkleRoot,
                        nonce = block.Nonce,
                        timestamp = block.Timestamp
                    });
                }
                else
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Block integrity check failed",
                        blockValid = isValid,
                        chainValid = isChainValid
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying blockchain");
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        #region Helper Methods

        // Helper method to generate PDF statement
        private async Task<byte[]> GenerateTransactionStatement(
            List<BlockchainTransaction> transactions,
            string memberNo,
            DateTime from,
            DateTime to)
        {
            // Implement PDF generation using iTextSharp or similar library
            // This is a placeholder - you'll need to add a PDF library
            throw new NotImplementedException("PDF generation not implemented yet");
        }

        // Helper method to validate block
        private async Task<bool> ValidateBlock(Block block)
        {
            try
            {
                // Recalculate block hash
                var blockData = $"{block.PreviousHash}{block.Timestamp:yyyy-MM-dd HH:mm:ss.fff}{block.MerkleRoot}{block.Nonce}";
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                var bytes = System.Text.Encoding.UTF8.GetBytes(blockData);
                var hashBytes = sha256.ComputeHash(bytes);
                var calculatedHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

                // Check if hash matches
                if (calculatedHash != block.BlockHash)
                    return false;

                // Check if block is confirmed
                if (!block.Confirmed)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion



        // Controllers/BlockchainController.cs - Add these methods
        #region Dashboard

        private string GetTimeAgo(DateTime timestamp)
        {
            var diff = DateTime.UtcNow - timestamp;
            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} hr ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} days ago";
            if (diff.TotalDays < 30) return $"{(int)(diff.TotalDays / 7)} weeks ago";
            return timestamp.ToString("MMM dd");
        }

        #endregion


    }
}