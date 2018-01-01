namespace EntityExtensions.Tests.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class level12 : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Employees", "Level1Id", c => c.Int());
            AddColumn("dbo.Employees", "Level2Id", c => c.Int());
            CreateIndex("dbo.Employees", "Level1Id");
            CreateIndex("dbo.Employees", "Level2Id");
            AddForeignKey("dbo.Employees", "Level1Id", "dbo.Employees", "Id");
            AddForeignKey("dbo.Employees", "Level2Id", "dbo.Employees", "Id");
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Employees", "Level2Id", "dbo.Employees");
            DropForeignKey("dbo.Employees", "Level1Id", "dbo.Employees");
            DropIndex("dbo.Employees", new[] { "Level2Id" });
            DropIndex("dbo.Employees", new[] { "Level1Id" });
            DropColumn("dbo.Employees", "Level2Id");
            DropColumn("dbo.Employees", "Level1Id");
        }
    }
}
