using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Genesis.Hardware;

namespace Genesis
{
    // ===================================================================
    //  Página "Hardware": CLP + módulos de IO.
    //  Partial de MainForm — acessa _projetoHardware, _carregando, _dicas.
    // ===================================================================
    internal partial class MainForm
    {
        private ComboBox _hwCmbClp;
        private Label _hwLblRefClp;
        private Label _hwLblOnboard;
        private RadioButton _hwRadRede;
        private RadioButton _hwRadTerminais;
        private FlowLayoutPanel _hwListaModulos;
        private Label _hwResumoTotal;

        // ===================================================================
        //  Criação da página
        // ===================================================================
        private Control CriarPaginaHardware()
        {
            var host = new Panel { AutoScroll = true, BackColor = Tema.Fundo };
            Win.Escurecer(host);
            var coluna = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Tema.Fundo,
                Padding = new Padding(0, 0, 0, 12)
            };

            coluna.Controls.Add(CartaoClp());
            coluna.Controls.Add(CartaoModulos());
            coluna.Controls.Add(CartaoInversores());
            coluna.Controls.Add(CartaoIhms());
            coluna.Controls.Add(CartaoRemotas());

            host.Controls.Add(coluna);
            return NovaPagina("Hardware", "CLP · módulos de IO",
                "Escolha o modelo do CLP, defina se o projeto é via PROFINET ou terminais, e adicione os módulos de IO (SB, SM ou de remotas ET200). "
                + "Esses dados definem quantos canais o projeto tem e como as receitas geram as tags.",
                new Control[]
                {
                    Tema.Botao("Recarregar", false, delegate { CarregarHardwareNaTela(); }, "abrir")
                }, host);
        }

        // ===================================================================
        //  Seção 1: CLP
        // ===================================================================
        private Control CartaoClp()
        {
            var card = new CartaoTabela { AutoSize = true, Margin = new Padding(0, 0, 0, 14), Dock = DockStyle.Fill };
            card.ColumnStyles[0].Width = 0;

            var t = Tema.TituloCartao("1 · CLP");
            t.Margin = new Padding(0, 0, 0, 12);
            card.Controls.Add(t, 1, 0);

            var linha1 = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, RowCount = 2, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 10) };
            linha1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
            linha1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
            linha1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            linha1.Controls.Add(Tema.Secao("Modelo"), 0, 0);
            _hwCmbClp = Tema.Lista(false);
            foreach (ClpInfo c in CatalogoClp.Todos) _hwCmbClp.Items.Add(c);
            _hwCmbClp.SelectedIndexChanged += delegate { AoMudarClp(); };
            Control quadroClp = Tema.Emoldurar(_hwCmbClp);
            quadroClp.Dock = DockStyle.Fill;
            quadroClp.Margin = new Padding(0, 0, 12, 0);
            linha1.Controls.Add(quadroClp, 0, 1);

            linha1.Controls.Add(Tema.Secao("Referência"), 1, 0);
            _hwLblRefClp = new Label
            {
                Text = "—",
                AutoSize = false,
                Height = Moldura.AlturaPadrao,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Tema.Mono,
                ForeColor = Tema.TextoSec,
                BackColor = Color.Transparent,
                Padding = new Padding(4, 0, 0, 0)
            };
            linha1.Controls.Add(_hwLblRefClp, 1, 1);

            linha1.Controls.Add(Tema.Secao("Onboard"), 2, 0);
            _hwLblOnboard = new Label
            {
                Text = "—",
                AutoSize = false,
                Height = Moldura.AlturaPadrao,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Tema.Pequena,
                ForeColor = Tema.TextoSec,
                BackColor = Color.Transparent
            };
            linha1.Controls.Add(_hwLblOnboard, 2, 1);
            card.Controls.Add(linha1, 1, 1);

            var linha2 = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent, Margin = new Padding(0, 4, 0, 0) };
            var rotCom = Tema.Secao("Comunicação");
            rotCom.Margin = new Padding(0, 0, 14, 0);
            linha2.Controls.Add(rotCom);
            _hwRadRede = new RadioButton
            {
                Text = "Via PROFINET",
                AutoSize = true,
                ForeColor = Tema.Texto,
                BackColor = Color.Transparent,
                Font = Tema.Normal,
                FlatStyle = FlatStyle.Standard,
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            _hwRadTerminais = new RadioButton
            {
                Text = "Via terminais",
                AutoSize = true,
                ForeColor = Tema.Texto,
                BackColor = Color.Transparent,
                Font = Tema.Normal,
                FlatStyle = FlatStyle.Standard,
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            _hwRadRede.CheckedChanged += delegate
            {
                if (_carregando || !_hwRadRede.Checked || _projetoHardware == null) return;
                _projetoHardware.ViaRede = true;
                MarcarModificado();
                AtualizarResumoHardware();
            };
            _hwRadTerminais.CheckedChanged += delegate
            {
                if (_carregando || !_hwRadTerminais.Checked || _projetoHardware == null) return;
                _projetoHardware.ViaRede = false;
                MarcarModificado();
                AtualizarResumoHardware();
            };
            linha2.Controls.Add(_hwRadRede);
            linha2.Controls.Add(_hwRadTerminais);
            card.Controls.Add(linha2, 1, 2);

            return card;
        }

        private void AoMudarClp()
        {
            if (_carregando) return;
            ClpInfo c = _hwCmbClp.SelectedItem as ClpInfo;
            if (c == null)
            {
                _hwLblRefClp.Text = "—";
                _hwLblOnboard.Text = "—";
                return;
            }
            _hwLblRefClp.Text = c.Referencia;
            _hwLblOnboard.Text = OnboardTexto(c);
            _projetoHardware = _projetoHardware ?? new ProjetoHardware();
            _projetoHardware.ModeloClp = c.Nome;
            _projetoHardware.ReferenciaClp = c.Referencia;
            MarcarModificado();
            AtualizarResumoHardware();
        }

        private static string OnboardTexto(ClpInfo c)
        {
            var partes = new List<string>();
            if (c.DiOnboard > 0) partes.Add(c.DiOnboard + " DI");
            if (c.DqOnboard > 0) partes.Add(c.DqOnboard + " DQ");
            if (c.AiOnboard > 0) partes.Add(c.AiOnboard + " AI");
            if (c.AqOnboard > 0) partes.Add(c.AqOnboard + " AQ");
            return partes.Count > 0 ? string.Join(" · ", partes.ToArray()) : "sem onboard";
        }

        // ===================================================================
        //  Seção 2: Módulos
        // ===================================================================
        private Control CartaoModulos()
        {
            var card = new CartaoTabela { AutoSize = true, Margin = new Padding(0, 0, 0, 14), Dock = DockStyle.Fill };
            card.ColumnStyles[0].Width = 0;

            var topo = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 10) };
            topo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            topo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            topo.Controls.Add(Tema.TituloCartao("2 · Módulos de IO"), 0, 0);
            var btnAdd = Tema.Botao("Adicionar módulo", false, delegate { AdicionarModulo(); }, "mais");
            btnAdd.AutoSize = false;
            btnAdd.Size = new Size(160, 34);
            btnAdd.Anchor = AnchorStyles.Right;
            topo.Controls.Add(btnAdd, 1, 0);
            card.Controls.Add(topo, 1, 0);

            _hwListaModulos = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            card.Controls.Add(_hwListaModulos, 1, 1);

            _hwResumoTotal = new Label
            {
                AutoSize = true,
                Font = Tema.Pequena,
                ForeColor = Tema.TextoSec,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 8, 0, 0)
            };
            card.Controls.Add(_hwResumoTotal, 1, 2);

            return card;
        }

        private void AdicionarModulo()
        {
            if (_projetoHardware == null)
            {
                Dialogo.Aviso(this, "Escolha o modelo do CLP primeiro (seção 1).");
                return;
            }
            using (var dlg = new EscolherModuloForm(_projetoHardware))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _projetoHardware.Modulos.Add(dlg.Modulo);
                MarcarModificado();
                ReconstruirListaModulos();
                ReconstruirListaInversores();
                ReconstruirListaIhms();
                ReconstruirListaRemotas();
                AtualizarResumoHardware();
            }
        }

        private void RemoverModulo(Modulo m)
        {
            if (_projetoHardware == null || m == null) return;
            _projetoHardware.Modulos.Remove(m);
            MarcarModificado();
            ReconstruirListaModulos();
            AtualizarResumoHardware();
        }

        private void ReconstruirListaModulos()
        {
            if (_hwListaModulos == null) return;
            _hwListaModulos.SuspendLayout();
            foreach (Control c in _hwListaModulos.Controls.Cast<Control>().ToList()) c.Dispose();
            _hwListaModulos.Controls.Clear();

            if (_projetoHardware == null || _projetoHardware.Modulos.Count == 0)
            {
                var vazio = Tema.Ajuda("Nenhum módulo adicionado ainda. Use \"Adicionar módulo\" acima.");
                vazio.ForeColor = Tema.TextoFraco;
                vazio.Margin = new Padding(0, 4, 0, 4);
                _hwListaModulos.Controls.Add(vazio);
            }
            else
            {
                foreach (Modulo m in _projetoHardware.Modulos.ToList())
                {
                    Modulo mm = m;
                    _hwListaModulos.Controls.Add(LinhaModulo(mm));
                }
            }
            _hwListaModulos.ResumeLayout(true);
        }

        private Control LinhaModulo(Modulo m)
        {
            ModuloInfo info = CatalogoModulos.PorReferencia(m.Referencia);
            string nome = info != null ? info.Nome : m.Referencia;
            string canais = "DI" + (m.Di * m.Quantidade) + " · DQ" + (m.Dq * m.Quantidade)
                + " · AI" + (m.Ai * m.Quantidade) + " · AQ" + (m.Aq * m.Quantidade);

            var linha = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 6),
                Width = 880
            };
            linha.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            linha.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            linha.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
            linha.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));

            linha.Controls.Add(new Label
            {
                Text = nome,
                AutoSize = true,
                Font = Tema.Negrito,
                ForeColor = Tema.Texto,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 8, 8, 4)
            }, 0, 0);

            var numQtd = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 16,
                Value = Math.Max(1, m.Quantidade),
                Width = 60,
                Height = 28,
                BackColor = Tema.Campo,
                ForeColor = Tema.Texto,
                Font = Tema.Normal,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 4, 8, 4)
            };
            numQtd.ValueChanged += delegate
            {
                if (_carregando) return;
                m.Quantidade = (int)numQtd.Value;
                MarcarModificado();
                ReconstruirListaModulos();
                AtualizarResumoHardware();
            };
            linha.Controls.Add(numQtd, 1, 0);

            linha.Controls.Add(new Label
            {
                Text = canais,
                AutoSize = false,
                Height = 28,
                TextAlign = ContentAlignment.MiddleRight,
                Font = Tema.Pequena,
                ForeColor = Tema.TextoSec,
                BackColor = Color.Transparent,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 8, 4)
            }, 2, 0);

            var btnX = Tema.BotaoIcone("lixeira", "Remover", delegate { RemoverModulo(m); }, _dicas);
            btnX.AutoSize = false;
            btnX.Size = new Size(30, 28);
            btnX.Margin = new Padding(0, 4, 0, 4);
            linha.Controls.Add(btnX, 3, 0);

            return linha;
        }

        // ===================================================================
        //  Resumo geral
        // ===================================================================
        private void AtualizarResumoHardware()
        {
            if (_hwResumoTotal == null) return;
            if (_projetoHardware == null)
            {
                _hwResumoTotal.Text = "";
                return;
            }
            var tot = TotaisHardware();
            string via = _projetoHardware.ViaRede ? "PROFINET" : "terminais";
            _hwResumoTotal.Text = "Total disponível: " + tot.Di + " DI · " + tot.Dq + " DQ · " + tot.Ai + " AI · " + tot.Aq + " AQ   ·   comunicação via " + via;
        }

        private (int Di, int Dq, int Ai, int Aq) TotaisHardware()
        {
            int di = 0, dq = 0, ai = 0, aq = 0;
            if (_projetoHardware == null) return (0, 0, 0, 0);

            ClpInfo c = CatalogoClp.PorReferencia(_projetoHardware.ReferenciaClp);
            if (c != null)
            {
                di += c.DiOnboard;
                dq += c.DqOnboard;
                ai += c.AiOnboard;
                aq += c.AqOnboard;
            }
            foreach (Modulo m in _projetoHardware.Modulos)
            {
                di += m.Di * m.Quantidade;
                dq += m.Dq * m.Quantidade;
                ai += m.Ai * m.Quantidade;
                aq += m.Aq * m.Quantidade;
            }
            foreach (RemotaEt200 r in _projetoHardware.RemotasEt200)
            {
                foreach (Modulo m in r.Modulos)
                {
                    di += m.Di * m.Quantidade;
                    dq += m.Dq * m.Quantidade;
                    ai += m.Ai * m.Quantidade;
                    aq += m.Aq * m.Quantidade;
                }
            }
            return (di, dq, ai, aq);
        }

        // ===================================================================
        //  Carregar / recarregar do projeto
        // ===================================================================
        private void CarregarHardwareNaTela()
        {
            if (_hwCmbClp == null) return;
            _carregando = true;
            try
            {
                ClpInfo c = _projetoHardware != null ? CatalogoClp.PorReferencia(_projetoHardware.ReferenciaClp) : null;
                _hwCmbClp.SelectedItem = c;
                if (c == null) { _hwCmbClp.SelectedIndex = -1; _hwLblRefClp.Text = "—"; _hwLblOnboard.Text = "—"; }
                _hwRadRede.Checked = _projetoHardware != null && _projetoHardware.ViaRede;
                _hwRadTerminais.Checked = _projetoHardware == null || !_projetoHardware.ViaRede;
                ReconstruirListaModulos();
                AtualizarResumoHardware();
            }
            finally
            {
                _carregando = false;
            }
        }
    }
}