# EFCodeGenerate
Entity Framework Mapping->Model 映射生成工具


**EFCodeGenerate是一个数据实体映射生成工具**+
适用C#,VB.NET，默认C#，如果需要生成VB.NET，需要自行调整，在类**OptimizeContextHandler**下*OptimizeContext*函数内。
我这里因为用的是Oracle，只用了Oracle的C#生。

## 生成结构
#### 脚本
```sql
create table ACTIVE_LISTINGS_REPORT
(
  id                              NUMBER(10) generated always as identity,
  sku                             VARCHAR2(200) not null,    
  item_condition                  NUMBER(10),    
  business_price                  NUMBER(10,2),
  create_time                     DATE default sysdate not null
)
```
#### 生成C# 代码 C# Model
```csharp
 public partial class ActiveListingsReport
    {
        public int Id { get; set; }
        public string Sku { get; set; } 
        public Nullable<int> ItemCondition { get; set; } 
        public Nullable<decimal> BusinessPrice { get; set; }
        public System.DateTime CreateTime { get; set; }
    }
```
#### 生成C# 代码 C# Mapping
```csharp
 public class ActiveListingsReportMap : EntityTypeConfiguration<ActiveListingsReport>
    {
        public ActiveListingsReportMap()
        {
            // Primary Key
            this.HasKey(t => t.Id);

            // Properties 字符串代码过滤大小
            this.Property(t => t.Sku)
                .IsRequired()
                .HasMaxLength(200);
            
            // Table & Column Mappings
            this.ToTable("ACTIVE_LISTINGS_REPORT");
            this.Property(t => t.Id).HasColumnName("ID");
            this.Property(t => t.Sku).HasColumnName("SKU");       
            this.Property(t => t.ItemCondition).HasColumnName("ITEM_CONDITION");            
            this.Property(t => t.BusinessPrice).HasColumnName("BUSINESS_PRICE");           
            this.Property(t => t.CreateTime).HasColumnName("CREATE_TIME");

        }
    }
```

**最后在不要忘记在DbContext 配置**
```csharp
 public class OracleDbContext : DbContext
    {
        //配置参考https://www.oracle.com/webfolder/technetwork/tutorials/obe/db/dotnet/NuGet/index.html
        public OracleDbContext()
            : base("OracleConnection")
        // base(OracleConnection(),true)
        {
            //this.Database.Log = msg => Debug.WriteLine(msg);
        }

        private static OracleConnection OracleConnection()
        {
            OracleConnection connection = null;
#if DEBUG
            connection = new OracleConnection(
                "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=tcp)(HOST=192.168.0.***)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=Schema)));User ID=jc_cc_001;Password=Jinchang001;");
#endif
            //添加

            return connection;
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            //指定数据库用户名，必须大写
            modelBuilder.HasDefaultSchema("USER_NAME");
           //添加Model->Table映射关系
            modelBuilder.Configurations.Add(new ActiveListingsReportMap());

            base.OnModelCreating(modelBuilder);
        }

        public DbSet<ActiveListingsReport> ActiveListingsReport { get; set; }

    }
  
```