import unittest
import import_stats

class MonthlyImportTests(unittest.TestCase):
    def test_unexpected_value_is_rejected(self):
        def char(t,x,y,w=5):return dict(t=t,x=x,y=y,w=w,h=10)
        pages=[dict(page=1,chars=[char('Test',370,781.4,20),char('Fealty',10,766.8,30),char('?',380,766.8)])]
        with self.assertRaisesRegex(ValueError,'Unexpected value'):
            import_stats.extract(pages)

    def test_seed_replaces_only_its_month_and_escapes_names(self):
        import sqlite3
        connection=sqlite3.connect(':memory:')
        row=dict(region="Mordred's Rest",category='Administration',name='Fealty',value=83,page=1)
        connection.executescript(import_stats.sql([row],2026,10))
        connection.executescript(import_stats.sql([row],2026,11))
        row['value']=90
        connection.executescript(import_stats.sql([row],2026,11))
        self.assertEqual([(10,83),(11,90)],connection.execute('SELECT Month,Value FROM WorldStatistic ORDER BY Month').fetchall())

if __name__=='__main__':unittest.main()
