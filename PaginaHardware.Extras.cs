using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Genesis.Hardware;

namespace Genesis
{
    // ===================================================================
    //  Seções 3 (inversores), 4 (IHMs) e 5 (remotas) da página Hardware.
    //  Partial de MainForm — complementa PaginaHardware.cs.
    // ===================================================================
    internal partial class MainForm
    {
        private FlowLayoutPanel _hwListaInversores;
        private FlowLayoutPanel _hwListaIhms;
        private FlowLayoutPanel _hwListaRemotas;

        // ===================================================================
        //  Seção 3: Inversores
        // ===================================================================
        private Control CartaoInversores()
        {
            var card = new CartaoTabela { AutoSize = true, Margin = new Padding(0, 0, 0, 14), Dock = DockStyle.Fill };
            card.ColumnStyles[0].Width = 0;

            var topo = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 10) };
            topo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            topo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            topo.Controls.Add(Tema.TituloCartao("3 · Inversores"), 0, 0);
            var btnAdd = Tema.Botao("Adicionar inversor", false, delegate { AdicionarInversor(); }, "mais");
            btnAdd.AutoSize = false;
            btnAdd.Size = new Size(160, 34);
            btnAdd.Anchor = AnchorStyles.Right;
            topo.Controls.Add(btnAdd, 1, 0);
            card.Controls.Add(topo, 1, 0);

            _hwListaInversores = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            card.Controls.Add(_hwListaInversores, 1, 1);
            return card;
        }

        private void AdicionarInversor()
        {
            if (_projetoHardware == null) { Dialogo.Aviso(this, "Escolha o CLP primeiro (seção 1)."); return; }
            using (var dlg = new InversorForm(_projetoHardware))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _projetoHardware.Inversores.Add(dlg.Inversor);
                MarcarModificado();
                ReconstruirListaInversores();
            }
        }

        private void EditarInversor(Inversor inv)
        {
            if (_projetoHardware == null || inv == null) return;
            using (var dlg = new InversorForm(_projetoHardware, inv))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                int idx = _projetoHardware.Inversores.IndexOf(inv);
                if (idx >= 0) _projetoHardware.Inversores[idx] = dlg.Inversor;
                MarcarModificado();
                ReconstruirListaInversores();
            }
        }

        private void RemoverInversor(Inversor inv)
        {
            if (_projetoHardware == null || inv == null) return;
            _projetoHardware.Inversores.Remove(inv);
            MarcarModificado();
            ReconstruirListaInversores();
        }

        private void ReconstruirListaInversores()
        {
            if (_hwListaInversores == null) return;
            _hwListaInversores.SuspendLayout();
            foreach (Control c in _hwListaInversores.Controls.Cast<Control>().ToList()) c.Dispose();
            _hwListaInversores.Controls.Clear();

            if (_projetoHardware == null || _projetoHardware.Inversores.Count == 0)
            {
                var vazio = Tema.Ajuda("Nenhum inversor. Use \"Adicionar inversor\" acima.");
                vazio.ForeColor = Tema.TextoFraco;
                vazio.Margin = new Padding(0, 4, 0, 4);
                _hwListaInversores.Controls.Add(vazio);
            }
            else
            {
                foreach (Inversor i in _projetoHardware.Inversores.ToList())
                {
                    Inversor ii = i;
                    _hwListaInversores.Controls.Add(LinhaInversor(ii));
                }
            }
            _hwListaInversores.ResumeLayout(true);
        }

        private Control LinhaInversor(Inversor inv)
        {
            var linha = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 5,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 6),
                Width = 880
            };
            linha.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
            linha.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            linha.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            linha.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            linha.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));

            linha.Controls.Add(new Label { Text = inv.Tag, AutoSize = true, Font = Tema.Negrito, ForeColor = Tema.Texto, BackColor = Color.Transparent, Margin = new Padding(0, 8, 8, 4) }, 0, 0);
            linha.Controls.Add(new Label { Text = inv.Modelo, AutoSize = true, Font = Tema.Pequena, ForeColor = Tema.TextoSec, BackColor = Color.Transparent, Margin = new Padding(0, 10, 8, 4) }, 1, 0);
            linha.Controls.Add(new Label { Text = "TG " + inv.Telegrama, AutoSize = true, Font = Tema.Pequena, ForeColor = Tema.TextoSec, BackColor = Color.Transparent, Margin = new Padding(0, 10, 8, 4) }, 2, 0);
            linha.Controls.Add(new Label { Text = string.IsNullOrEmpty(inv.IpAddress) ? "(sem IP)" : inv.IpAddress, AutoSize = true, Font = Tema.Mono, ForeColor = string.IsNullOrEmpty(inv.IpAddress) ? Tema.Amarelo : Tema.TextoSec, BackColor = Color.Transparent, Margin = new Padding(0, 10, 8, 4) }, 3, 0);

            var botoes = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Color.Transparent, Margin = new Padding(0) };
            var btnEdit = Tema.BotaoIcone("config", "Editar", delegate { EditarInversor(inv); }, _dicas);
            btnEdit.AutoSize = false;
            btnEdit.Size = new Size(28, 28);
            btnEdit.Margin = new Padding(0, 2, 4, 0);
            botoes.Controls.Add(btnEdit);
            var btnX = Tema.BotaoIcone("lixeira", "Remover", delegate { RemoverInversor(inv); }, _dicas);
            btnX.AutoSize = false;
            btnX.Size = new Size(28, 28);
            btnX.Margin = new Padding(0, 2, 0, 0);
            botoes.Controls.Add(btnX);
            linha.Controls.Add(botoes, 4, 0);

            return linha;
        }

        // ===================================================================
        //  Seção 4: IHMs
        // ===================================================================
        private Control CartaoIhms()
        {
            var card = new CartaoTabela { AutoSize = true, Margin = new Padding(0, 0, 0, 14), Dock = DockStyle.Fill };
            card.ColumnStyles[0].Width = 0;

            var topo = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 10) };
            topo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            topo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            topo.Controls.Add(Tema.TituloCartao("4 · IHMs"), 0, 0);
            var btnAdd = Tema.Botao("Adicionar IHM", false, delegate { AdicionarIhm(); }, "mais");
            btnAdd.AutoSize = false;
            btnAdd.Size = new Size(160, 34);
            btnAdd.Anchor = AnchorStyles.Right;
            topo.Controls.Add(btnAdd, 1, 0);
            card.Controls.Add(topo, 1, 0);

            _hwListaIhms = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent, Margin = new Padding(0) };
            card.Controls.Add(_hwListaIhms, 1, 1);
            return card;
        }

        private void AdicionarIhm()
        {
            if (_projetoHardware == null) { Dialogo.Aviso(this, "Escolha o CLP primeiro (seção 1)."); return; }
            using (var dlg = new IhmForm(_projetoHardware))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _projetoHardware.Ihs.Add(dlg.Ihm);
                MarcarModificado();
                ReconstruirListaIhms();
            }
        }

        private void EditarIhm(Ihm ihm)
        {
            if (_projetoHardware == null || ihm == null) return;
            using (var dlg = new IhmForm(_projetoHardware, ihm))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                int idx = _projetoHardware.Ihs.IndexOf(ihm);
                if (idx >= 0) _projetoHardware.Ihs[idx] = dlg.Ihm;
                MarcarModificado();
                ReconstruirListaIhms();
            }
        }

        private void RemoverIhm(Ihm ihm)
        {
            if (_projetoHardware == null || ihm == null) return;
            _projetoHardware.Ihs.Remove(ihm);
            MarcarModificado();
            ReconstruirListaIhms();
        }

        private void ReconstruirListaIhms()
        {
            if (_hwListaIhms == null) return;
            _hwListaIhms.SuspendLayout();
            foreach (Control c in _hwListaIhms.Controls.Cast<Control>().ToList()) c.Dispose();
            _hwListaIhms.Controls.Clear();

            if (_projetoHardware == null || _projetoHardware.Ihs.Count == 0)
            {
                var vazio = Tema.Ajuda("Nenhuma IHM. Use \"Adicionar IHM\" acima.");
                vazio.ForeColor = Tema.TextoFraco;
                vazio.Margin = new Padding(0, 4, 0, 4);
                _hwListaIhms.Controls.Add(vazio);
            }
            else
            {
                foreach (Ihm i in _projetoHardware.Ihs.ToList())
                {
                    Ihm ii = i;
                    _hwListaIhms.Controls.Add(LinhaIhm(ii));
                }
            }
            _hwListaIhms.ResumeLayout(true);
        }

        private Control LinhaIhm(Ihm ihm)
        {
            IhmInfo info = CatalogoIhms.PorReferencia(ihm.Referencia);
            string modelo = info != null ? info.Modelo : ihm.Referencia;

            var linha = new TableLayoutPanel { AutoSize = true, ColumnCount = 4, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 6), Width = 880 };
            linha.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            linha.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            linha.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            linha.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));

            linha.Controls.Add(new Label { Text = ihm.Tag, AutoSize = true, Font = Tema.Negrito, ForeColor = Tema.Texto, BackColor = Color.Transparent, Margin = new Padding(0, 8, 8, 4) }, 0, 0);
            linha.Controls.Add(new Label { Text = modelo, AutoSize = true, Font = Tema.Pequena, ForeColor = Tema.TextoSec, BackColor = Color.Transparent, Margin = new Padding(0, 10, 8, 4) }, 1, 0);
            linha.Controls.Add(new Label { Text = string.IsNullOrEmpty(ihm.IpAddress) ? "(sem IP)" : ihm.IpAddress, AutoSize = true, Font = Tema.Mono, ForeColor = string.IsNullOrEmpty(ihm.IpAddress) ? Tema.Amarelo : Tema.TextoSec, BackColor = Color.Transparent, Margin = new Padding(0, 10, 8, 4) }, 2, 0);

            var botoes = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Color.Transparent, Margin = new Padding(0) };
            var btnEdit = Tema.BotaoIcone("config", "Editar", delegate { EditarIhm(ihm); }, _dicas);
            btnEdit.AutoSize = false; btnEdit.Size = new Size(28, 28); btnEdit.Margin = new Padding(0, 2, 4, 0);
            botoes.Controls.Add(btnEdit);
            var btnX = Tema.BotaoIcone("lixeira", "Remover", delegate { RemoverIhm(ihm); }, _dicas);
            btnX.AutoSize = false; btnX.Size = new Size(28, 28); btnX.Margin = new Padding(0, 2, 0, 0);
            botoes.Controls.Add(btnX);
            linha.Controls.Add(botoes, 3, 0);
            return linha;
        }

        // ===================================================================
        //  Seção 5: Remotas ET200 (com sub-form para módulos)
        // ===================================================================
        private Control CartaoRemotas()
        {
            var card = new CartaoTabela { AutoSize = true, Margin = new Padding(0, 0, 0, 14), Dock = DockStyle.Fill };
            card.ColumnStyles[0].Width = 0;

            var topo = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 10) };
            topo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            topo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            topo.Controls.Add(Tema.TituloCartao("5 · Remotas ET200"), 0, 0);
            var btnAdd = Tema.Botao("Adicionar remota", false, delegate { AdicionarRemota(); }, "mais");
            btnAdd.AutoSize = false;
            btnAdd.Size = new Size(160, 34);
            btnAdd.Anchor = AnchorStyles.Right;
            topo.Controls.Add(btnAdd, 1, 0);
            card.Controls.Add(topo, 1, 0);

            _hwListaRemotas = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent, Margin = new Padding(0) };
            card.Controls.Add(_hwListaRemotas, 1, 1);
            return card;
        }

        private void AdicionarRemota()
        {
            if (_projetoHardware == null) { Dialogo.Aviso(this, "Escolha o CLP primeiro (seção 1)."); return; }
            using (var dlg = new RemotaForm(_projetoHardware))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _projetoHardware.RemotasEt200.Add(dlg.Remota);
                MarcarModificado();
                ReconstruirListaRemotas();
                AtualizarResumoHardware();
            }
        }

        private void EditarRemota(RemotaEt200 r)
        {
            if (_projetoHardware == null || r == null) return;
            using (var dlg = new RemotaForm(_projetoHardware, r))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                int idx = _projetoHardware.RemotasEt200.IndexOf(r);
                if (idx >= 0) _projetoHardware.RemotasEt200[idx] = dlg.Remota;
                MarcarModificado();
                ReconstruirListaRemotas();
                AtualizarResumoHardware();
            }
        }

        private void RemoverRemota(RemotaEt200 r)
        {
            if (_projetoHardware == null || r == null) return;
            _projetoHardware.RemotasEt200.Remove(r);
            MarcarModificado();
            ReconstruirListaRemotas();
            AtualizarResumoHardware();
        }

        private void ReconstruirListaRemotas()
        {
            if (_hwListaRemotas == null) return;
            _hwListaRemotas.SuspendLayout();
            foreach (Control c in _hwListaRemotas.Controls.Cast<Control>().ToList()) c.Dispose();
            _hwListaRemotas.Controls.Clear();

            if (_projetoHardware == null || _projetoHardware.RemotasEt200.Count == 0)
            {
                var vazio = Tema.Ajuda("Nenhuma remota. Use \"Adicionar remota\" acima. Os módulos de cada remota ficam dentro dela.");
                vazio.ForeColor = Tema.TextoFraco;
                vazio.Margin = new Padding(0, 4, 0, 4);
                _hwListaRemotas.Controls.Add(vazio);
            }
            else
            {
                foreach (RemotaEt200 r in _projetoHardware.RemotasEt200.ToList())
                {
                    RemotaEt200 rr = r;
                    _hwListaRemotas.Controls.Add(LinhaRemota(rr));
                }
            }
            _hwListaRemotas.ResumeLayout(true);
        }

        private Control LinhaRemota(RemotaEt200 r)
        {
            var linha = new TableLayoutPanel { AutoSize = true, ColumnCount = 5, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 6), Width = 880 };
            linha.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            linha.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            linha.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            linha.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            linha.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));

            linha.Controls.Add(new Label { Text = r.Tag, AutoSize = true, Font = Tema.Negrito, ForeColor = Tema.Texto, BackColor = Color.Transparent, Margin = new Padding(0, 8, 8, 4) }, 0, 0);
            linha.Controls.Add(new Label { Text = r.ReferenciaInterface, AutoSize = true, Font = Tema.Pequena, ForeColor = Tema.TextoSec, BackColor = Color.Transparent, Margin = new Padding(0, 10, 8, 4) }, 1, 0);
            linha.Controls.Add(new Label { Text = string.IsNullOrEmpty(r.IpAddress) ? "(sem IP)" : r.IpAddress, AutoSize = true, Font = Tema.Mono, ForeColor = string.IsNullOrEmpty(r.IpAddress) ? Tema.Amarelo : Tema.TextoSec, BackColor = Color.Transparent, Margin = new Padding(0, 10, 8, 4) }, 2, 0);
            linha.Controls.Add(new Label { Text = "(" + r.Modulos.Count + " módulo" + (r.Modulos.Count == 1 ? "" : "s") + ")", AutoSize = true, Font = Tema.Pequena, ForeColor = Tema.TextoSec, BackColor = Color.Transparent, Margin = new Padding(0, 10, 8, 4) }, 3, 0);

            var botoes = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Color.Transparent, Margin = new Padding(0) };
            var btnEdit = Tema.BotaoIcone("config", "Editar (módulos)", delegate { EditarRemota(r); }, _dicas);
            btnEdit.AutoSize = false; btnEdit.Size = new Size(28, 28); btnEdit.Margin = new Padding(0, 2, 4, 0);
            botoes.Controls.Add(btnEdit);
            var btnX = Tema.BotaoIcone("lixeira", "Remover", delegate { RemoverRemota(r); }, _dicas);
            btnX.AutoSize = false; btnX.Size = new Size(28, 28); btnX.Margin = new Padding(0, 2, 0, 0);
            botoes.Controls.Add(btnX);
            linha.Controls.Add(botoes, 4, 0);
            return linha;
        }
    }
}