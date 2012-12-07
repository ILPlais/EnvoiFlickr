using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;
using FlickrNet;

namespace EnvoiFlickr
{
    /// <summary>
    /// Logique d'interaction pour MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Déclaration des constantes
        private const String clefFlickr = "caafdbc81b5242ce72359f183fa97414";
        private const String secretFlickr = "97624892c344cba6";

        // Déclaration du BackgroundWorker
        private BackgroundWorker transfertFlickr;

        // Déclaration des variables
        private Flickr conFlickr = new Flickr(clefFlickr, secretFlickr);
        private Auth autFlickr = new Auth();
        private String tempFrob;
        private string[] extensions = { "*.jp*g", "*.gif", "*.png", "*.tif*", 
                                        "*.avi", "*.wmv", "*.mov", "*.mp*g*", 
                                        "*.3gp", "*.m2ts", "*.ogg", "*.ogv" };
        private List<String> listeImages = new List<String>();
        private List<String> listeAlbums = new List<String>();
        private String idAlbumSelect;
        private String nomAlbum = String.Empty;
        private Boolean isPublic = false;
        private Boolean isFriend = false;
        private Boolean isFamily = false;
        private String tempTitre = String.Empty;
        private String tempDescription = String.Empty;
        private Boolean isGeolocalise = false;
        private double[] coordonnees;

        public MainWindow()
        {
            InitializeComponent();

            // Charge le dossier Images par défaut
            txtDossier.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

            // Création du BackgroundWorker
            transfertFlickr = new BackgroundWorker();

            // Indique la fonction à traiter lors du déclanchement de l'évenement DoWork
            transfertFlickr.DoWork += new DoWorkEventHandler(EnvoiFlickr);

            // Indique la fonction de traitement lors du déclanchement de l'évenement indiquant un changement de progression
            transfertFlickr.ProgressChanged += new ProgressChangedEventHandler(flickrProgressionEnvoiTotal);
            transfertFlickr.WorkerReportsProgress = true;

            // Indique que le BackgroundWorker peut être interrompu
            transfertFlickr.WorkerSupportsCancellation = true;

            // Indique la fonction à exécuter à la fin du transfert
            transfertFlickr.RunWorkerCompleted += new RunWorkerCompletedEventHandler(transfertFlickr_RunWorkerCompleted);

            // Paramètre la fonction gérant la progression de l'envoi
            conFlickr.OnUploadProgress += new EventHandler<UploadProgressEventArgs>(flickrProgressionEnvoiPhoto);
        }

        private void btnSuivant_Click(object sender, RoutedEventArgs e)
        {
            if (tbcAssistant.SelectedIndex != tbcAssistant.Items.Count - 1)
            {
                // Change d'onglet
                tbcAssistant.SelectedIndex++;
                changeOnglet(tbcAssistant);
            }
            else
            {
                if (transfertFlickr.IsBusy)
                {
                    // Demande confirmation
                    if (MessageBox.Show("Voulez-vous annuler le transfert des images ?", "Annulation",
                        MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes) == MessageBoxResult.Yes)
                    {
                        // Annule le transfert
                        transfertFlickr.CancelAsync();
                        taskBarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Paused;
                        MessageBox.Show("Le transfert est en cours d'annulation. Veuillez patienter…", "Annulation",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    // Ferme l'application
                    this.Close();
                }
            }
        }

        private void btnPrecedent_Click(object sender, RoutedEventArgs e)
        {
            // Change d'onglet
            tbcAssistant.SelectedIndex--;
            changeOnglet(tbcAssistant);
        }

        private void changeOnglet(object sender)
        {
            // Active si besoin le bouton "Précédent"
            btnPrecedent.IsEnabled = tbcAssistant.SelectedIndex > 0;

            // Active si besoin le bouton "Suivant"
            btnSuivant.IsEnabled = (tbcAssistant.SelectedIndex < tbcAssistant.Items.Count - 1);

            // Change le texte du label
            lblEtape.Content = String.Format("Étape n°{0}", tbcAssistant.SelectedIndex + 1);

            // Lance l'envoi lors du passage à l'onglet n°4
            if (tbcAssistant.SelectedIndex == 3)
            {
                envoiFlickr();
            }
        }

        private void btnConnexion_Click(object sender, RoutedEventArgs e)
        {
            // Récupère le Frob pour l'autentification à Flickr
            tempFrob = conFlickr.AuthGetFrob();

            // Récupère l'URI pour la connexion à Flickr
            String flickrURI = conFlickr.AuthCalcUrl(tempFrob, AuthLevel.Write);

            // Lance le navigateur pour l'autorisation
            System.Diagnostics.Process.Start(flickrURI);

            // Active le bouton de validation
            btnValidation.IsEnabled = true;
            btnValidation.Focus();
        }

        private void btnValidation_Click(object sender, RoutedEventArgs e)
        {
            // Récupère l'autentification
            try
            {
                autFlickr = conFlickr.AuthGetToken(tempFrob);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Une erreur vient de ce produire :" + Environment.NewLine + ex.Message,
                    "Erreur d'authentification à Flickr", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Active le bouton "Suivant"
            btnSuivant.IsEnabled = true;

            // Affiche le quota restant
            if (conFlickr.PeopleGetUploadStatus().IsPro == false)
            {
                lblQuota.Content = String.Format("Quota restant sur votre compte Flickr : {0} Kio",
                    conFlickr.PeopleGetUploadStatus().BandwidthRemainingKB);
            }
            else
            {
                lblQuota.Content = "Quota restant sur votre compte Flickr : ∞ Kio";
            }

            // Affiche un message de bienvenue
            MessageBox.Show("Bienvenue " + conFlickr.PeopleGetUploadStatus().UserName + " !", "Bienvenue",
                MessageBoxButton.OK, MessageBoxImage.Information);

            // Récupère la liste des albums
            cmbAlbum.Items.Clear();
            listeAlbums.Clear();
            cmbAlbum.Items.Add("Sélectionnez un album");
            listeAlbums.Add("-1");
            cmbAlbum.Items.Add("Ajoutez un nouvel album…");
            listeAlbums.Add("0");
            PhotosetCollection lstAlbums = conFlickr.PhotosetsGetList();
            foreach (Photoset album in lstAlbums)
            {
                cmbAlbum.Items.Add(album.Title);
                listeAlbums.Add(album.PhotosetId);
            }
            cmbAlbum.SelectedIndex = 0;
        }

        private void btnDossier_Click(object sender, RoutedEventArgs e)
        {
            // Affiche la boîte de dialogue de sélection d'un dossier
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "Sélectionnez le dossier contenant les photos à envoyer sur Flickr :";
            dialog.ShowNewFolderButton = false;
            dialog.SelectedPath = txtDossier.Text;
            System.Windows.Forms.DialogResult result = dialog.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK)
            {
                // Récupère le chemin du dossier
                txtDossier.Text = dialog.SelectedPath;
            }
        }

        private void chkTitre_Checked(object sender, RoutedEventArgs e)
        {
            // Active la saisie du titre si besoin
            txtTitre.IsEnabled = chkTitre.IsChecked.Value;
        }

        private void chkDescription_Checked(object sender, RoutedEventArgs e)
        {
            // Active la saisie de la description
            txtDescription.IsEnabled = chkDescription.IsChecked.Value;
        }

        private void chkPosition_Checked(object sender, RoutedEventArgs e)
        {
            // Active les champs de saisie de la position si besoin
            lblLatitude.IsEnabled = chkPosition.IsChecked.Value;
            txtLatitude.IsEnabled = chkPosition.IsChecked.Value;
            lblDegreeLatitude.IsEnabled = chkPosition.IsChecked.Value;
            lblLongitude.IsEnabled = chkPosition.IsChecked.Value;
            txtLongitude.IsEnabled = chkPosition.IsChecked.Value;
            lblDegreeLongitude.IsEnabled = chkPosition.IsChecked.Value;
        }

        private void envoiFlickr()
        {
            // Désactive le bouton Précédent
            btnPrecedent.IsEnabled = false;

            // Change le bouton Suivant en bouton Annuler
            btnSuivant.Content = "Annuler";
            btnSuivant.IsEnabled = true;
            btnSuivant.IsCancel = true;

            // Vide la liste des images à envoyer
            lbEnvoi.Items.Clear();
            listeImages.Clear();

            // Réinitialise les barres de progression
            pbProgressionPhoto.Value = 0;
            pbProgressionTotal.Value = 0;

            // Parcours le dossier sélectionné
            parcoursRepertoire(txtDossier.Text);

            // Remplit la liste
            foreach (String fichier in listeImages)
            {
                lbEnvoi.Items.Add(fichier);
            }

            // Spécifie les limites de la barre de progression total
            pbProgressionTotal.Maximum = listeImages.Count();

            // Récupère la visibilité des images
            switch (cmbVisibilite.SelectedIndex)
            {
                // Tout le monde
                case 0:
                    isPublic = true;
                    isFriend = true;
                    isFamily = true;
                    break;
                // Votre famille
                case 1:
                    isPublic = false;
                    isFriend = false;
                    isFamily = true;
                    break;
                // Vos amis
                case 2:
                    isPublic = false;
                    isFriend = true;
                    isFamily = false;
                    break;
                // Votre famille et vos amis
                case 3:
                    isPublic = false;
                    isFriend = false;
                    isFamily = true;
                    break;
                // Uniquement vous
                case 4:
                    isPublic = false;
                    isFriend = false;
                    isFamily = false;
                    break;
            }

            // Récupère le titre des images
            if (chkTitre.IsChecked.Value)
            {
                tempTitre = txtTitre.Text;
            }

            // Récupère la description des images
            if (chkDescription.IsChecked.Value)
            {
                tempDescription = txtDescription.Text;
            }

            // Récupère le nom de l'album
            if (idAlbumSelect == "0")
            {
                nomAlbum = txtNomAlbum.Text;
            }

            isGeolocalise = chkPosition.IsChecked.Value;

            if (isGeolocalise)
            {
                // Récupère la position des photos
                coordonnees = new double[] { Convert.ToDouble(txtLatitude.Text), Convert.ToDouble(txtLongitude.Text) };
            }

            // Démarre la barre de progression dans la barre de tâches
            taskBarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;

            // Lance le BackgroundWorker
            transfertFlickr.RunWorkerAsync();
        }

        private void parcoursRepertoire(String repertoire)
        {
            // Récupère les fichiers du répertoire
            foreach (String extension in extensions)
            {
                foreach (String fichierCourant in System.IO.Directory.GetFiles(repertoire, extension))
                {
                    listeImages.Add(fichierCourant);
                }
            }

            // Parcours si besoin les sous répertoires
            if (chkRecrusive.IsChecked.Value)
            {
                foreach (String sousDossier in System.IO.Directory.GetDirectories(repertoire))
                {
                    parcoursRepertoire(sousDossier);
                }
            }
        }

        private void cmbAlbum_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Si la liste n'est pas vide
            if (listeAlbums.Count != 0)
            {
                // Affiche si besoin le champ de saisie du nom du nouvel album
                if (listeAlbums[cmbAlbum.SelectedIndex] == "0")
                {
                    txtNomAlbum.Visibility = System.Windows.Visibility.Visible;
                    txtNomAlbum.Focus();
                }
                else
                {
                    txtNomAlbum.Visibility = System.Windows.Visibility.Hidden;
                }

                // Récupère l'id de l'album sélectionné
                idAlbumSelect = listeAlbums[cmbAlbum.SelectedIndex];
            }
        }

        // Fonction de traitement de l'envoi vers Flickr
        private void EnvoiFlickr(object data, DoWorkEventArgs e)
        {
            String photoId = String.Empty;
            int i = 0;

            foreach (String image in listeImages)
            {
                if (!transfertFlickr.CancellationPending)
                {
                    try
                    {
                        // Indique au BackgroundWorker qu'il y a eu progression 
                        transfertFlickr.ReportProgress(i);

                        // Envoi l'image sur Flickr
                        photoId = conFlickr.UploadPicture(image, tempTitre, tempDescription, String.Empty,
                            isPublic, isFamily, isFriend);

                        // Si la demande d'ajout à un album est faite
                        if (idAlbumSelect != "-1")
                        {
                            if (idAlbumSelect == "0")
                            {
                                // S'il s'agit d'un nouvel album : le créer
                                idAlbumSelect = conFlickr.PhotosetsCreate(nomAlbum, photoId).PhotosetId;
                            }
                            else
                            {
                                // Sinon, ajouter la photo à l'album existant
                                conFlickr.PhotosetsAddPhoto(idAlbumSelect, photoId);
                            }
                        }

                        // Si la demande de géolocalisation est spécifiée
                        if (isGeolocalise)
                        {
                            conFlickr.PhotosGeoSetLocation(photoId, coordonnees[0], coordonnees[1]);
                        }

                        i++;
                    }
                    catch (FlickrException Ex)
                    {
                        MessageBox.Show("Une erreur c'est produite lors de l'envoi de l'image vers Flickr."
                            + Environment.NewLine + Ex.Message, "Erreur d'envoi d'une image sur Flickr",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    catch (Exception Ex)
                    {
                        MessageBox.Show("Une super erreur c'est produite lors de l'envoi de l'image vers Flickr."
                            + Environment.NewLine + Ex.Message, "Super erreur d'envoi d'une image sur Flickr",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                else
                {
                    e.Cancel = true;
                }
            }
        }

        // Fonction appellée lors de la progression de l'envoi d'une photo
        private void flickrProgressionEnvoiPhoto(object sender, UploadProgressEventArgs progression)
        {
            // Change la valeur de la barre de progression
            //pbProgressionPhoto.Value = progression.ProcessPercentage;
        }

        // Fonction appellée lors de la progression de l'envoi total
        private void flickrProgressionEnvoiTotal(object sender, ProgressChangedEventArgs e)
        {
            // Mise à jour de la ProgressBar
            pbProgressionTotal.Value = e.ProgressPercentage + 1;
            taskBarItemInfo.ProgressValue = (e.ProgressPercentage + 1) / pbProgressionTotal.Maximum;

            // Mise à jour de la sélection dans la liste
            lbEnvoi.SelectedIndex = e.ProgressPercentage;
        }

        // Fonction exécutée lors de la fin du transfert
        void transfertFlickr_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // Change le bouton Annuler en bouton Quitter
            btnSuivant.Content = "Quitter";

            if (e.Error != null)
            {
                // En cas d'erreur
                taskBarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Error;
                MessageBox.Show("Une erreur c'est produite lors du transfert des images vers Flickr."
                    + Environment.NewLine + e.Error.ToString(),
                    "Erreur lors du transfert vers Flickr", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                taskBarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;

                if (e.Cancelled == false)
                {
                    // Message indiquant que le transfert est fini
                    if (MessageBox.Show("Le transfert des images vers Flickr est fini."
                        + Environment.NewLine + "Voulez-vous afficher votre galerie ?",
                        "Fin du transfert", MessageBoxButton.YesNo, MessageBoxImage.Information)
                        == MessageBoxResult.Yes)
                    {
                        System.Diagnostics.Process.Start(conFlickr.UrlsGetUserPhotos());
                    }
                }
                else
                {
                    // Message indiquant que le transfert a été annulé
                    MessageBox.Show("Le transfert des images vers Flickr a été annulé.",
                        "Fin du transfert", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }
}
