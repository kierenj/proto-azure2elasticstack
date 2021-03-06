﻿// <auto-generated />
using azure2elasticstack;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using System;

namespace azure2elasticstack.Migrations
{
    [DbContext(typeof(EfContext))]
    [Migration("20180301085511_InitialCreate")]
    partial class InitialCreate
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "2.0.1-rtm-125");

            modelBuilder.Entity("azure2elasticstack.MetricLastDate", b =>
                {
                    b.Property<string>("Key")
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(500);

                    b.Property<DateTime>("LastDate");

                    b.HasKey("Key");

                    b.ToTable("MetricLastDates");
                });
#pragma warning restore 612, 618
        }
    }
}
