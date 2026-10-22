using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Models;
using Microsoft.EntityFrameworkCore;

namespace House.Of.Arbitration.Data;

public class DatabaseService : IDatabaseService
{
    private readonly AppDbContext _context;

    public DatabaseService(AppDbContext context)
    {
        _context = context;
    }

    public async Task ResetUserDataAsync()
    {
        await _context.ResetUserDataAsync();
    }

    public async Task ImportCompetitionDataAsync(CompetitionModel competition)
    {
        if (competition == null) return;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.ChangeTracker.Clear();

            // 1. Countries
            var countries = new Dictionary<string, CountryModel>(StringComparer.OrdinalIgnoreCase);

            void CollectCountry(CompetitorModel? c)
            {
                if (c?.Country != null && !string.IsNullOrWhiteSpace(c.Country.IsoCode) && !countries.ContainsKey(c.Country.IsoCode))
                {
                    countries[c.Country.IsoCode] = c.Country;
                }
            }

            // 2. AgeRanges
            var ageRanges = new Dictionary<int, AgeRangeModel>();

            void CollectAgeRange(AgeRangeModel? a)
            {
                if (a != null && a.Id > 0 && !ageRanges.ContainsKey(a.Id))
                {
                    ageRanges[a.Id] = a;
                }
            }

            // 3. Competitors
            var competitors = new Dictionary<int, CompetitorModel>();

            void CollectCompetitor(CompetitorModel? c)
            {
                if (c != null && c.Id > 0)
                {
                    if (!competitors.ContainsKey(c.Id))
                    {
                        competitors[c.Id] = c;
                    }
                    CollectCountry(c);
                }
            }

            if (competition.Categories != null)
            {
                foreach (var cat in competition.Categories)
                {
                    CollectAgeRange(cat.AgeRange);

                    if (cat.Competitors != null)
                    {
                        foreach (var cc in cat.Competitors)
                        {
                            CollectCompetitor(cc.Competitor);
                        }
                    }

                    if (cat.Draw != null)
                    {
                        if (cat.Draw.DrawKnockouts != null)
                        {
                            foreach (var k in cat.Draw.DrawKnockouts)
                            {
                                CollectCompetitor(k.Competitor1);
                                CollectCompetitor(k.Competitor2);
                                CollectCompetitor(k.Winner);
                                CollectCompetitor(k.Looser);
                            }
                        }

                        if (cat.Draw.DrawPools != null)
                        {
                            foreach (var p in cat.Draw.DrawPools)
                            {
                                CollectCompetitor(p.Competitor1);
                                CollectCompetitor(p.Competitor2);
                                CollectCompetitor(p.Winner);
                                CollectCompetitor(p.Looser);
                            }
                        }

                        if (cat.Draw.DrawOrders != null)
                        {
                            foreach (var o in cat.Draw.DrawOrders)
                            {
                                CollectCompetitor(o.Competitor);
                            }
                        }
                    }
                }
            }

            // Save new countries if any
            foreach (var country in countries.Values)
            {
                var existingCountry = await _context.Countries.FirstOrDefaultAsync(c => c.IsoCode == country.IsoCode);
                if (existingCountry == null)
                {
                    _context.Countries.Add(new CountryModel
                    {
                        IsoCode = country.IsoCode,
                        Name = country.Name
                    });
                }
            }
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            // Save new age ranges if any
            foreach (var ar in ageRanges.Values)
            {
                var existingAr = await _context.AgeRanges.FirstOrDefaultAsync(a => a.Id == ar.Id);
                if (existingAr == null)
                {
                    _context.AgeRanges.Add(new AgeRangeModel
                    {
                        Id = ar.Id,
                        Label = ar.Label,
                        MinAge = ar.MinAge,
                        MaxAge = ar.MaxAge
                    });
                }
            }
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            // 4. Save Competition
            var existingComp = await _context.Competitions.FirstOrDefaultAsync(c => c.Id == competition.Id);
            if (existingComp != null)
            {
                existingComp.Name = competition.Name;
                existingComp.Date = competition.Date;
                existingComp.Type = competition.Type;
            }
            else
            {
                _context.Competitions.Add(new CompetitionModel
                {
                    Id = competition.Id,
                    Name = competition.Name,
                    Date = competition.Date,
                    Type = competition.Type
                });
            }
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            // 5. Save Competitors
            foreach (var comp in competitors.Values)
            {
                var existingCompEntity = await _context.Competitors.FirstOrDefaultAsync(c => c.Id == comp.Id);
                var isoCode = comp.CountryIsoCode ?? comp.Country?.IsoCode;
                if (existingCompEntity != null)
                {
                    existingCompEntity.FirstName = comp.FirstName;
                    existingCompEntity.LastName = comp.LastName;
                    existingCompEntity.Club = comp.Club;
                    existingCompEntity.BirthDate = comp.BirthDate;
                    existingCompEntity.Genre = comp.Genre;
                    existingCompEntity.Weight = comp.Weight;
                    existingCompEntity.CountryIsoCode = isoCode;
                    existingCompEntity.Photo = comp.Photo;
                }
                else
                {
                    _context.Competitors.Add(new CompetitorModel
                    {
                        Id = comp.Id,
                        FirstName = comp.FirstName,
                        LastName = comp.LastName,
                        Club = comp.Club,
                        BirthDate = comp.BirthDate,
                        Genre = comp.Genre,
                        Weight = comp.Weight,
                        CountryIsoCode = isoCode,
                        Photo = comp.Photo
                    });
                }
            }
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            // 6. Save Categories
            if (competition.Categories != null)
            {
                foreach (var cat in competition.Categories)
                {
                    var existingCat = await _context.Categories.FirstOrDefaultAsync(c => c.Id == cat.Id);
                    int? arId = cat.AgeRangeId ?? cat.AgeRange?.Id;
                    if (existingCat != null)
                    {
                        existingCat.Type = cat.Type;
                        existingCat.Genre = cat.Genre;
                        existingCat.WeightMin = cat.WeightMin;
                        existingCat.WeightMax = cat.WeightMax;
                        existingCat.AgeRangeId = arId;
                        existingCat.RoundType = cat.RoundType;
                        existingCat.CompetitionId = competition.Id;
                    }
                    else
                    {
                        _context.Categories.Add(new CategoryModel
                        {
                            Id = cat.Id,
                            Type = cat.Type,
                            Genre = cat.Genre,
                            WeightMin = cat.WeightMin,
                            WeightMax = cat.WeightMax,
                            AgeRangeId = arId,
                            RoundType = cat.RoundType,
                            CompetitionId = competition.Id
                        });
                    }
                }
                await _context.SaveChangesAsync();
                _context.ChangeTracker.Clear();

                // 7. Save CompetitorCategories and Draws
                foreach (var cat in competition.Categories)
                {
                    if (cat.Competitors != null)
                    {
                        foreach (var cc in cat.Competitors)
                        {
                            int compId = cc.CompetitorId > 0 ? cc.CompetitorId : (cc.Competitor?.Id ?? 0);
                            if (compId > 0)
                            {
                                var existingCc = await _context.CompetitorCategories.FirstOrDefaultAsync(x => (cc.Id > 0 && x.Id == cc.Id) || (x.CategoryId == cat.Id && x.CompetitorId == compId));
                                if (existingCc != null)
                                {
                                    existingCc.CategoryId = cat.Id;
                                    existingCc.CompetitorId = compId;
                                }
                                else
                                {
                                    _context.CompetitorCategories.Add(new CompetitorCategoryModel
                                    {
                                        Id = cc.Id,
                                        CategoryId = cat.Id,
                                        CompetitorId = compId
                                    });
                                }
                            }
                        }
                    }

                    if (cat.Draw != null)
                    {
                        var draw = cat.Draw;
                        var existingDraw = await _context.Draws.FirstOrDefaultAsync(d => (draw.Id > 0 && d.Id == draw.Id) || d.CategoryId == cat.Id);
                        if (existingDraw != null)
                        {
                            existingDraw.CategoryId = cat.Id;
                            draw.Id = existingDraw.Id;
                        }
                        else
                        {
                            _context.Draws.Add(new DrawModel
                            {
                                Id = draw.Id,
                                CategoryId = cat.Id
                            });
                        }
                    }
                }
                await _context.SaveChangesAsync();
                _context.ChangeTracker.Clear();

                // 8. Save DrawKnockouts, DrawPools, DrawOrders
                foreach (var cat in competition.Categories)
                {
                    if (cat.Draw != null)
                    {
                        int drawId = cat.Draw.Id;

                        if (cat.Draw.DrawKnockouts != null)
                        {
                            foreach (var k in cat.Draw.DrawKnockouts)
                            {
                                var existingK = await _context.DrawsKnockouts.FirstOrDefaultAsync(x => x.Id == k.Id);
                                int? comp1Id = k.Competitor1Id ?? k.Competitor1?.Id;
                                int? comp2Id = k.Competitor2Id ?? k.Competitor2?.Id;
                                int? winnerId = k.WinnerId ?? k.Winner?.Id;
                                int? looserId = k.LooserId ?? k.Looser?.Id;

                                if (existingK != null)
                                {
                                    existingK.Order = k.Order;
                                    existingK.GlobalOrder = k.GlobalOrder;
                                    existingK.DrawId = drawId;
                                    existingK.Competitor1Id = comp1Id;
                                    existingK.Competitor2Id = comp2Id;
                                    existingK.WinnerId = winnerId;
                                    existingK.LooserId = looserId;
                                    existingK.IsFinished = k.IsFinished;
                                    existingK.CurrentRound = k.CurrentRound;
                                    existingK.RoundResultsJson = k.RoundResultsJson ?? string.Empty;
                                }
                                else
                                {
                                    _context.DrawsKnockouts.Add(new DrawKnockoutModel
                                    {
                                        Id = k.Id,
                                        Order = k.Order,
                                        GlobalOrder = k.GlobalOrder,
                                        DrawId = drawId,
                                        Competitor1Id = comp1Id,
                                        Competitor2Id = comp2Id,
                                        WinnerId = winnerId,
                                        LooserId = looserId,
                                        IsFinished = k.IsFinished,
                                        CurrentRound = k.CurrentRound,
                                        RoundResultsJson = k.RoundResultsJson ?? string.Empty
                                    });
                                }
                            }
                        }

                        if (cat.Draw.DrawPools != null)
                        {
                            foreach (var p in cat.Draw.DrawPools)
                            {
                                var existingP = await _context.DrawsPools.FirstOrDefaultAsync(x => x.Id == p.Id);
                                int? comp1Id = p.Competitor1Id ?? p.Competitor1?.Id;
                                int? comp2Id = p.Competitor2Id ?? p.Competitor2?.Id;
                                int? winnerId = p.WinnerId ?? p.Winner?.Id;
                                int? looserId = p.LooserId ?? p.Looser?.Id;

                                if (existingP != null)
                                {
                                    existingP.Order = p.Order;
                                    existingP.GlobalOrder = p.GlobalOrder;
                                    existingP.DrawId = drawId;
                                    existingP.Competitor1Id = comp1Id;
                                    existingP.Competitor2Id = comp2Id;
                                    existingP.WinnerId = winnerId;
                                    existingP.LooserId = looserId;
                                    existingP.IsFinished = p.IsFinished;
                                    existingP.CurrentRound = p.CurrentRound;
                                    existingP.RoundResultsJson = p.RoundResultsJson ?? string.Empty;
                                }
                                else
                                {
                                    _context.DrawsPools.Add(new DrawPoolsModel
                                    {
                                        Id = p.Id,
                                        Order = p.Order,
                                        GlobalOrder = p.GlobalOrder,
                                        DrawId = drawId,
                                        Competitor1Id = comp1Id,
                                        Competitor2Id = comp2Id,
                                        WinnerId = winnerId,
                                        LooserId = looserId,
                                        IsFinished = p.IsFinished,
                                        CurrentRound = p.CurrentRound,
                                        RoundResultsJson = p.RoundResultsJson ?? string.Empty
                                    });
                                }
                            }
                        }

                        if (cat.Draw.DrawOrders != null)
                        {
                            foreach (var o in cat.Draw.DrawOrders)
                            {
                                var existingO = await _context.DrawsOrders.FirstOrDefaultAsync(x => x.Id == o.Id);
                                int? compId = o.CompetitorId ?? o.Competitor?.Id;

                                if (existingO != null)
                                {
                                    existingO.Order = o.Order;
                                    existingO.GlobalOrder = o.GlobalOrder;
                                    existingO.DrawId = drawId;
                                    existingO.CompetitorId = compId;
                                    existingO.IsFinished = o.IsFinished;
                                    existingO.CurrentRound = o.CurrentRound;
                                    existingO.RoundResultsJson = o.RoundResultsJson ?? string.Empty;
                                }
                                else
                                {
                                    _context.DrawsOrders.Add(new DrawOrderModel
                                    {
                                        Id = o.Id,
                                        Order = o.Order,
                                        GlobalOrder = o.GlobalOrder,
                                        DrawId = drawId,
                                        CompetitorId = compId,
                                        IsFinished = o.IsFinished,
                                        CurrentRound = o.CurrentRound,
                                        RoundResultsJson = o.RoundResultsJson ?? string.Empty
                                    });
                                }
                            }
                        }
                    }
                }
                await _context.SaveChangesAsync();
                _context.ChangeTracker.Clear();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            _context.ChangeTracker.Clear();
        }
    }
}
