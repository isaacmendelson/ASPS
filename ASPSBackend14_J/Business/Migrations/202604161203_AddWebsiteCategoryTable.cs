using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Business.Migrations
{
    /// <inheritdoc />
    public partial class AddWebsiteCategoryTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create WebsiteCategories table if not exists
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS WebsiteCategories (
                    `Key` VARCHAR(36) NOT NULL,
                    `Name` VARCHAR(100) NOT NULL,
                    `ParentId` VARCHAR(36) NULL,
                    `DateCreated` DATETIME(6) NOT NULL,
                    `DateModified` DATETIME(6) NULL,
                    `DateDeleted` DATETIME(6) NULL,
                    `IsDeleted` BIT NOT NULL DEFAULT 0,
                    `IsDisabled` BIT NOT NULL DEFAULT 0,
                    `Source` VARCHAR(100) NULL,
                    PRIMARY KEY (`Key`),
                    UNIQUE INDEX `IX_WebsiteCategories_Name` (`Name`),
                    INDEX `IX_WebsiteCategories_ParentId` (`ParentId`),
                    INDEX `IX_WebsiteCategories_DateDeleted` (`DateDeleted`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
            ");

            // Seed data for WebsiteCategory
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO WebsiteCategories (`Key`, `Name`, `ParentId`, `DateCreated`, `DateModified`, `DateDeleted`, `IsDeleted`, `IsDisabled`, `Source`) VALUES
                ('cat-banking', 'banking', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-credit_union', 'credit_union', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-insurance', 'insurance', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-investment', 'investment', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-stock_trading', 'stock_trading', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-crypto_exchange', 'crypto_exchange', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-payment_service', 'payment_service', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-lending', 'lending', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-ecommerce', 'ecommerce', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-marketplace', 'marketplace', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-auction', 'auction', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-classifieds', 'classifieds', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-grocery', 'grocery', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-fashion', 'fashion', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-electronics', 'electronics', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-government', 'government', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-municipality', 'municipality', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-military', 'military', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-court', 'court', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-tax_authority', 'tax_authority', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-public_service', 'public_service', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-hospital', 'hospital', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-clinic', 'clinic', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-pharmacy', 'pharmacy', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-telehealth', 'telehealth', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-mental_health', 'mental_health', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-university', 'university', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-school', 'school', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-online_course', 'online_course', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-elearning', 'elearning', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-streaming', 'streaming', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-gaming', 'gaming', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-gambling', 'gambling', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-sports_betting', 'sports_betting', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-adult_content', 'adult_content', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-news', 'news', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-blog', 'blog', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-forum', 'forum', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-social_network', 'social_network', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-messaging', 'messaging', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-legal', 'legal', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-accounting', 'accounting', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-real_estate', 'real_estate', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-travel', 'travel', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-job_board', 'job_board', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-saas', 'saas', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-cloud', 'cloud', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-web_hosting', 'web_hosting', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-vpn_proxy', 'vpn_proxy', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-developer_tools', 'developer_tools', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-restaurant', 'restaurant', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-automotive', 'automotive', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-pets', 'pets', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-nonprofit', 'nonprofit', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-language_learning', 'language_learning', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-review_directory', 'review_directory', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-ride_delivery', 'ride_delivery', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json'),
                ('cat-religious', 'religious', '', '2026-01-01 00:00:00', NULL, NULL, 0, 0, 'category_patterns.json');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS WebsiteCategories;");
        }
    }
}
